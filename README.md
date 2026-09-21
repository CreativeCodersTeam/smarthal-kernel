# SmartHal Kernel

SmartHal is a self-hosted home automation system. This repository holds the **kernel**: the .NET
solution with the server process, the three published contract packages and the internal projects
everything else builds on.

The current state is **Slice 0 — process and repository foundation**: the scaffold is in place and the
projects are intentionally empty. Behaviour, contracts and adapters arrive in later slices.

## Layout

| Directory | Content |
|---|---|
| `src/` | The seven production projects |
| `tests/` | The nine test projects (one unit test project per production project, plus integration and architecture tests) |

### Production projects

| Project | May reference | Published as a package |
|---|---|---|
| `SmartHal.Contracts` | — | yes |
| `SmartHal.Adapter.Sdk` | `Contracts` | yes |
| `SmartHal.Automation.Sdk` | `Contracts` | yes |
| `SmartHal.Core.Abstractions` | `Adapter.Sdk`, `Contracts` | no |
| `SmartHal.Core` | `Core.Abstractions`, `Adapter.Sdk`, `Contracts` | no |
| `SmartHal.Server` | `Core`, `Core.Abstractions`, `Adapter.Sdk`, `Contracts` | no |
| `SmartHal.Cli` | `Contracts` | no |

The reference topology is exhaustive: any reference between `SmartHal.*` production projects that is
not listed above is disallowed. The architecture tests enforce this.

## Prerequisites

- .NET SDK **10.0.x**. `global.json` pins the band with `rollForward: latestFeature`, so a side by side
  installation of SDK 8 is not used.

## Commands

```bash
dotnet restore SmartHal.slnx
dotnet build SmartHal.slnx --no-restore
dotnet test SmartHal.slnx --no-build
dotnet pack SmartHal.slnx --no-build -o artifacts/packages
```

## Configuration

The server reads its own settings from the `SmartHal` section. The section is bound and validated
once while the process starts — before the first hosted service runs. An invalid configuration ends
the process with exit code `2` and writes one `1103` log event per violation, naming the section,
the field and the broken rule. No configured value is ever repeated in such a message.

| Key | Type | Required | Default | Validation |
| --- | --- | --- | --- | --- |
| `SmartHal:InstanceName` | `string` | yes | — | not empty, not whitespace only, at most 64 characters |
| `SmartHal:DataDirectory` | `string` | yes | — | a valid path; created when missing; has to be writable |
| `SmartHal:ShutdownTimeout` | `TimeSpan` | no | `00:00:30` | greater than zero, at most `00:05:00` |

The data directory is created when it does not exist yet; that is reported as log event `1104`. If
it cannot be created or cannot be written to, the configuration counts as invalid.

Like every other key, these take part in the configuration layering, so the usual sources apply. As
`SMARTHAL_`-prefixed environment variables — `__` separates the section from the field:

```bash
export SMARTHAL_SmartHal__InstanceName=home
export SMARTHAL_SmartHal__DataDirectory=/var/lib/smarthal
export SMARTHAL_SmartHal__ShutdownTimeout=00:01:00
dotnet SmartHal.Server.dll
```

Or on the command line, which wins over the environment:

```bash
dotnet SmartHal.Server.dll --SmartHal:InstanceName=home --SmartHal:DataDirectory=/var/lib/smarthal
```

The shipped `appsettings.json` carries an empty `SmartHal` section, so neither mandatory value has a
default. The host takes its content root from the working directory of the process: starting the
server from a directory without an `appsettings.json` and without the two `SMARTHAL_*` variables
above therefore ends with exit code `2` — which is the intended answer, not a defect.

## Configure logging

The server logs through `ILogger<T>` only; Serilog sits behind it as the provider and is built
entirely from the `Serilog` section of the configuration. The section takes part in the normal
configuration layering, so every value below can be changed in `appsettings.json`, in the file named
by `SMARTHAL_CONFIG_FILE`, through a `SMARTHAL_`-prefixed environment variable or on the command
line — without rebuilding.

**Two sinks.** The console writes compact JSON in every environment except `Development`, where
`appsettings.Development.json` replaces the formatter with readable text. The second sink is a
rolling file.

**Log level per component.** `Serilog:MinimumLevel:Default` sets the floor, and
`Serilog:MinimumLevel:Override:<component>` raises or lowers a single component. The component is
the logger category, that is the namespace-qualified type name behind `ILogger<T>`, and a prefix
matches every category below it:

```bash
dotnet SmartHal.Server.dll \
  --Serilog:MinimumLevel:Default=Warning \
  --Serilog:MinimumLevel:Override:SmartHal.Server.Composition=Debug
```

`appsettings.Development.json` already raises `SmartHal` to `Debug`. A component name contains dots,
which the `__` separator of an environment variable cannot express, so an override is set on the
command line or in one of the JSON files rather than through `SMARTHAL_…`.

**File path and rolling.** The file sink writes to `./logs/smarthal-<date>.log`, relative to the
working directory of the process and independent of `SmartHal:DataDirectory`. It rolls daily and
keeps 14 files; both values are configurable:

| Key | Default | Meaning |
| --- | --- | --- |
| `Serilog:WriteTo:File:Args:path` | `./logs/smarthal-.log` | Base name; the date is inserted before the extension |
| `Serilog:WriteTo:File:Args:rollingInterval` | `Day` | How often a new file is started |
| `Serilog:WriteTo:File:Args:retainedFileCountLimit` | `14` | How many files are kept |

**Correlation.** Every event carries the `TraceId` and `SpanId` of the running `System.Diagnostics`
activity — fields `@tr` and `@sp` of the JSON output. There is no separate correlation identifier.
`SmartHal.Server.Diagnostics.ServerTelemetry` holds the `ActivitySource` and the `Meter` of the
server, both named after the assembly. No OpenTelemetry SDK and no exporter is wired up.

## Readiness

The server reports whether it can do its work through health checks. Every check carries both tags,
`live` (the process is alive) and `ready` (the process can work); the readiness state is formed from
the `ready` checks alone. Slice 0 registers one check, `configuration`, which passes as soon as the
`SmartHal` section is bound and validated.

**Three states.** The process starts in _starting_ and stays there until a readiness check has passed
for the first time — a process that has never been ready cannot become _not ready_. Afterwards every
change between passing and failing checks is a transition:

| State | Meaning | EventId | Level |
| --- | --- | --- | --- |
| starting | The host runs, but no readiness check has passed yet | `1200` `HealthStarting` | Information |
| ready | Every readiness check passes | `1201` `HealthReady` | Information |
| not ready | At least one readiness check no longer passes | `1202` `HealthNotReady` | Warning |

`1200` is written once, when the first result arrives. `1201` and `1202` are written on each change
and never repeated while the state stays the same, so a healthy process stays quiet. `1202` names the
reported status and the checks that fail.

**Schedule.** The checks are published half a second after the start and every 30 seconds afterwards,
so a normal start reaches `1201` about half a second after `1001`.

**No transport.** There is no HTTP endpoint, no readiness file and no socket. Inside the process the
state is readable through `HealthCheckService`; from outside it is visible in the log alone. A
transport is not part of this slice and is planned for slice 14.

## Conventions

- **Language rule:** identifiers in code are **English**. Documentation — this README, XML doc
  comments, code comments and the justifications in `.editorconfig` — is **English** as well
  (user decision D-10, which overrides the German documentation rule of the specification).
- **Terminology:** only the canonical terms from `CONTEXT.md` in the documentation repository are
  used. The words listed there under _Avoid_ are avoided; that list is binding and is checked in
  review, not by a tool.
- **Target framework:** every project targets `net10.0`. Upgrades only ever go to the next LTS; STS
  releases are skipped.
- **Package versions:** managed centrally in `Directory.Packages.props`. No `.csproj` ever carries a
  version.
- **Analyzers:** the .NET analyzers (`AnalysisLevel=latest-Recommended`), `Roslynator.Analyzers` and
  `SonarAnalyzer.CSharp`. **Warnings are errors** — locally and in CI, without exception.
- **Rule suppressions:** only in `.editorconfig` and only with a justifying comment. Point
  suppressions in code are allowed only as `#pragma warning disable` with a justification on the
  line immediately above.
- **Test stack:** xUnit v3, FakeItEasy and AwesomeAssertions, extended by ArchUnitNET for the
  architecture tests and `Microsoft.Extensions.Diagnostics.Testing` for log assertions. No other
  test, mock or assertion library is used.
- **Test names:** unit and architecture tests use `MethodName_Scenario_ExpectedResult`, integration
  tests use `Given…_When…_Then…`.

## Versioning and releases

The repository carries a single version: the server, the CLI and the three published packages are
built from the same commit and stamped with the same number. GitVersion derives that number from the
git history — it is maintained nowhere by hand, so a build needs the full history (`fetch-depth: 0`
in CI). A release is a tag `v<SemVer>` on `main`; `main` is always releasable and work happens in
short-lived `feature/*` branches, whose name becomes the prerelease label.

[`VERSIONING.md`](VERSIONING.md) is the binding reference: it holds the branching and release rules,
the exhaustive table of what counts as a breaking change, and the checklist a review of the three
published packages works through.

The packages are checked with the acceptance script:

```bash
dotnet pack SmartHal.slnx -c Release -o artifacts/packages
eng/verify-packages.sh artifacts/packages
```

It exits 0 only when all five checks report `PASS`. It needs `unzip`, `python3` and the .NET SDK, and
runs unchanged on Linux and macOS.

## Documentation

The specification, architecture decision records, the glossary (`CONTEXT.md`) and the development
plans live in the separate documentation repository
[`CreativeCodersTeam/smarthal-project`](https://github.com/CreativeCodersTeam/smarthal-project).
This repository deliberately carries no domain documentation.

## License

Apache-2.0 — see [LICENSE](LICENSE).
