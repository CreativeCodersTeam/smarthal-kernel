# Improvement Proposals — smarthal-kernel

**Date:** 2026-10-02
**Branch:** `feature/device-catalog` (clean working tree, HEAD `cd19623`)
**Scope:** the whole repository: `src/` (~8,100 LOC in 7 projects), `tests/` (~11,800 LOC), and the build and repository setup
**Method:** a design review along the `code-design` principles (SRP, SoC, DRY, KISS, YAGNI, seams). Every proposal names the principle it rests on and the counter-principle it was checked against.
**Baseline (verified):** `dotnet build SmartHal.slnx` → 0 warnings, 0 errors. `dotnet test --solution SmartHal.slnx` → 1063/1063 passed.

No repository file was changed apart from this report. Statements marked **FACT** were verified against the source or by running the built server (Debug build, `src/SmartHal.Server/bin/Debug/net10.0`). Statements marked **UNVERIFIED** are hypotheses.

---

## Summary

| ID | Proposal | Area | Priority | Risk of the change | Effort |
|---|---|---|---|---|---|
| S-1 | Map every start-up failure to a defined exit code | Server | High | Low | S |
| S-2 | Stop the silent start without any configuration or log when the working directory differs | Server | High | Medium | S |
| S-3 | Report each missing option once: drop the redundant DataAnnotations layer | Server | High | Low–Medium | S |
| S-4 | Remove the hidden options validation (with file system side effects) from `builder.Build()` | Server | Medium | Low | S |
| Q-1 | Add the CI pipeline that README and build files already assume | Repo | High | Low | M |
| T-1 | Scope `--ignore-exit-code 8` to the test projects that are really empty | Tests | Medium | Low | S |
| C-1 | One home for "resolve a channel profile and check it" (R14) | Core | Medium | Low | S |
| C-2 | Use `TypeRef` as the catalog key instead of the `(string Name, int Major)` tuple | Core | Medium | Low | S |
| C-3 | Give validation paths a type of their own | Core | Low–Medium | Low | M |
| S-5 | Split `ServerHost.RunAsync` into its steps | Server | Medium | Low | S |
| K-1 | Pin the catalog's copies of enum knowledge, remove local duplicates | Catalog | Medium | Very low | S |
| S-6 | Drop duplicate lifecycle log lines of `Microsoft.Hosting.Lifetime` | Server | Low | Low | S |
| S-7 | Delete the unused `HealthStateMonitor.Current` | Server | Low | Very low | XS |
| T-2 | Keep one copy of `RepositoryLocator` | Tests | Low | Very low | XS |
| C-4 | Format the profile messages invariantly like every other rule | Core | Low | Very low | XS |
| X-1 | Decide the casing of the `BusMessage` discriminators before the first release | Contracts | Low (now) | High (contract) | XS |

Effort: XS < 30 min, S < half a day, M ≈ 1 day.

**Recommended order:** S-1 + S-5 together (same method), S-2, S-3, S-4, then T-1 and Q-1. The Core proposals C-1/C-2 can go in one small refactoring PR. C-3 is optional and only pays off if the validator keeps growing.

---

## High priority

### S-1 — Map every start-up failure to a defined exit code

**Location:** `src/SmartHal.Server/Composition/ServerHost.cs:57-74` (try block), `:91` (`builder.Build()`), `:93` (logger resolution)

**Finding.** The `try/catch` of step 1 only covers `Host.CreateApplicationBuilder` and `ConfigurationStack.Apply`. `builder.Build()` and the first resolution of `ILoggerFactory` run outside any handler. Serilog builds its logger lazily from configuration inside `Build()`, so an invalid `Serilog` section crashes the process with an unhandled exception.

**FACT** (probe): with `SMARTHAL_Serilog__WriteTo__Console__Args__formatter__type="Bogus.Formatter, Bogus"`, the server ends with **exit code 134 (SIGABRT)** and a raw stack trace. The trace shows `SerilogSetup.cs:line 54` → `ServerHost.cs:line 91`. `ExitCodes` documents only 0, 1 and 2, and `InvalidConfiguration` (2) is documented as "the configuration could not be built". A process supervisor (systemd, Kubernetes) therefore sees an undocumented code.

**Proposed change.**
- Put `builder.Build()` and the logger resolution under the same handling as step 1: write to stderr (no logger exists yet) and return `ExitCodes.InvalidConfiguration`, because the cause is configuration.
- Optionally wrap `host.StopAsync` (step 8) the same way: today any exception other than `TimeoutException` propagates out of `RunAsync` unhandled. That should map to `ExitCodes.UnhandledError`.

```csharp
IHost host;
try
{
    host = builder.Build();
    logger = host.Services.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(ServerHost));
}
catch (Exception exception)   // same rationale as step 1: no logger exists yet
{
    await Console.Error.WriteLineAsync($"The SmartHal host could not be built: {exception.Message}").ConfigureAwait(false);
    return ExitCodes.InvalidConfiguration;
}
```

**Benefit.** Every way the process can end maps to a documented exit code. Operators get one readable stderr line instead of a 60-line stack trace and SIGABRT.
**Risk: Low.** It only adds handling. Add an integration test that starts with a broken Serilog formatter and expects exit code 2 (`ServerProcessRunner` already supports out-of-process runs).
**Principle.** Robustness at the composition root. Checked against KISS: one extra `try`, no new type.

---

### S-2 — Stop the silent start without any configuration or log when the working directory differs

**Location:** `src/SmartHal.Server/Composition/ConfigurationStack.cs:70-77`

**Finding.** `appsettings.json` and `appsettings.{Environment}.json` are loaded with `optional: true`, relative to the content root. The generic host sets the content root to the **current working directory**, not to the application directory. The comment says "1 - appsettings.json, always", but the code makes the file optional.

**FACT** (probe): started from another directory (`cd /tmp/x && dotnet …/SmartHal.Server.dll`), the server ends with **exit code 2 and writes 0 bytes** to stdout and stderr. It found no `appsettings.json`, so it had no Serilog sinks and no `SmartHal` section. The validation then fails, and nobody can see why. Started from the output directory, the same binary runs normally.

**Proposed change** (pick one; the user should decide which, because it changes deployment behaviour):
1. **Recommended:** resolve the JSON files against `AppContext.BaseDirectory`, for example `Host.CreateApplicationBuilder(new HostApplicationBuilderSettings { Args = args, ContentRootPath = AppContext.BaseDirectory })`. The files ship next to the binary (`CopyToOutputDirectory`), so that is where they belong.
2. Make `appsettings.json` `optional: false`. A missing file then fails in step 1 with a readable stderr line and exit code 2. This is the smaller change, but it still depends on the working directory.

**Benefit.** No silent failure mode. The server behaves the same no matter which directory it is started from.
**Risk: Medium.** A deployment that deliberately places `appsettings.json` in the working directory instead of next to the binary would change behaviour with option 1. `SMARTHAL_CONFIG_FILE` (source 3) remains the documented way to supply an external file. `ConfigurationLayeringTests` must be checked.
**Principle.** Fail fast and visibly. Checked against YAGNI: nothing is added, a default is corrected.

---

### S-3 — Report each missing option once: drop the redundant DataAnnotations layer

**Location:** `src/SmartHal.Server/Configuration/SmartHalOptions.cs:34,44` (`[Required]`), `src/SmartHal.Server/Configuration/SmartHalOptionsValidator.cs:77-84,117-124`, `src/SmartHal.Server/Composition/ServiceRegistration.cs:47`, `src/SmartHal.Server/Configuration/OptionsFailure.cs:57-66,85-88`, `src/SmartHal.Server/SmartHal.Server.csproj:16`

**Finding.** `InstanceName` and `DataDirectory` are checked twice: by `[Required]` through `ValidateDataAnnotations()`, and by `SmartHalOptionsValidator` with `IsNullOrWhiteSpace`. The custom check is strictly stronger, because it also rejects whitespace.

**FACT** (probe, empty configuration): the log contains **four** `ConfigurationInvalid` (1103) events for two missing fields:
```
InstanceName  - The InstanceName field is required.
DataDirectory - The DataDirectory field is required.
InstanceName  - must be set and must not consist of whitespace only.
DataDirectory - must be set and must not consist of whitespace only.
```
To support both validators, `OptionsFailure` parses the English, version-dependent message text of `Microsoft.Extensions.Options.DataAnnotations` with a regex (`DataAnnotationsMessage`). That couples the server to a message format of the framework.

**Proposed change.** Remove `[Required]`, `.ValidateDataAnnotations()`, the `Microsoft.Extensions.Options.DataAnnotations` package reference and the DataAnnotations branch of `OptionsFailure.ParseMessage`, together with its tests. `SmartHalOptionsValidator` already covers every rule. If a structured result is wanted, the validator could expose its failures as `(Field, Reason)` pairs and skip the text round trip completely. That is optional.

**Benefit.** One message per violation (the purpose of FR-25). One validator instead of two. One dependency and one fragile regex fewer.
**Risk: Low–Medium.** The code change is small. However, the comments cite FR-22 ("`ValidateDataAnnotations()` checks the attributes") — **this needs a spec decision**. If FR-22 must stay, the alternative is the opposite cut: keep `[Required]` and remove the custom "must be set" checks. The whitespace check then needs `[RegularExpression(@"\S")]` or similar, and the regex parsing remains.
**Principle.** DRY (one rule, one home) and KISS. Rejected alternative: deduplicating the messages in `OptionsFailure.Parse` treats the symptom and keeps both validators.

---

### Q-1 — Add the CI pipeline that README and build files already assume

**Location:** repository root (no `.github/` directory), `README.md:166,181`, `Directory.Build.props:34-37`, `VERSIONING.md:23`, `eng/verify-packages.sh`

**Finding.** **FACT:** the repository contains no CI definition. Even so, `README.md:166` promises "Warnings are errors — locally and in CI". `Directory.Build.props` switches on `ContinuousIntegrationBuild` for `GITHUB_ACTIONS`. `VERSIONING.md` gives a rule for CI clones (full fetch depth). `eng/verify-packages.sh` is written for a Linux CI. None of these gates runs automatically today.

**Proposed change.** A single GitHub Actions workflow on `pull_request` and `push` to `main`, with these steps: `actions/checkout` with `fetch-depth: 0` (GitVersion), `actions/setup-dotnet` from `global.json`, `dotnet build SmartHal.slnx -c Release`, `dotnet test --solution SmartHal.slnx -c Release --no-build`, `dotnet format --verify-no-changes`, and `dotnet pack` followed by `eng/verify-packages.sh artifacts/packages`.

**Benefit.** The quality bar the repository already defines (warnings as errors, 1063 tests, architecture tests, package acceptance) is enforced on every change instead of depending on discipline.
**Risk: Low.** It is a new file and touches no product code. Pushing or activating it is a remote operation and is left to the user.
**Principle.** Not a code principle — this closes the gap between the documented and the actual process.

---

## Medium priority

### S-4 — Remove the hidden options validation (with file system side effects) from `builder.Build()`

**Location:** `src/SmartHal.Server/Hosting/ShutdownTimeoutConfigurator.cs:46-59`, `src/SmartHal.Server/Configuration/SmartHalOptionsValidator.cs:140-161`

**Finding.** `ShutdownTimeoutConfigurator` reads `IOptions<SmartHalOptions>.Value` while the host builds its `HostOptions`. That triggers the **complete** options validation, including `SmartHalOptionsValidator.ValidateDataDirectory`, which creates the data directory and writes a probe file. This happens during `builder.Build()`, before step 4, and a validation failure is swallowed in an empty `catch`.

**FACT** (probe, valid configuration with a new data directory): the log line `DataDirectoryCreated` (1104) is written **before** `HostStarting` (1000), although the start sequence documents validation as step 4.
**UNVERIFIED:** `IOptions<T>` (UnnamedOptionsManager) and `ValidateOnStart` (IOptionsMonitor cache) keep separate caches, so the validator — and with it the directory probe — probably runs twice per start.

**Proposed change.** Let the configurator read only the one raw value it needs and leave all validation to step 4:

```csharp
public sealed class ShutdownTimeoutConfigurator(IConfiguration configuration) : IConfigureOptions<HostOptions>
{
    public void Configure(HostOptions options)
    {
        try
        {
            if (configuration.GetSection(SmartHalOptions.SectionName)
                    .GetValue<TimeSpan?>(nameof(SmartHalOptions.ShutdownTimeout)) is { } timeout
                && timeout > TimeSpan.Zero)
            {
                options.ShutdownTimeout = timeout;
            }
        }
        catch (InvalidOperationException)
        {
            // An unparsable value is reported by step 4 of the start sequence.
        }
    }
}
```

The range check (≤ 5 min) does not need to be repeated, because an out-of-range value aborts the start in step 4 before any stop happens.

**Alternative (UNVERIFIED, larger):** remove the configurator and pass the configured timeout as a cancellation token to `host.StopAsync` in step 8. `Host.StopAsync` links the passed token with `HostOptions.ShutdownTimeout`, so that limit would have to be raised for the token to be the effective bound.

**Benefit.** Validation runs exactly once, in the step that owns it. No file system side effects at build time. No swallowed exception. The log order matches the documented sequence.
**Risk: Low.** `ShutdownTests` (including the AC-13 timeout test) cover the behaviour.
**Principle.** SoC: configuring the host is not validating options. Moving directory provisioning out of the validator (SRP) was considered too. It is defensible, but the spec text on `SmartHalOptions.DataDirectory` ("created when it does not exist") makes that a user decision. It is not needed for this fix.

---

### T-1 — Scope `--ignore-exit-code 8` to the test projects that are really empty

**Location:** `tests/Directory.Build.props:10-17`

**Finding.** Exit code 8 ("no tests ran") is ignored for **all** test projects, so the four intentionally empty projects (`Adapter.Sdk`, `Automation.Sdk`, `Cli`, `Core.Abstractions` UnitTests) do not break `dotnet test`. As a side effect, a populated project whose tests all disappear — broken discovery, a wrong filter, an accidental exclusion — still reports green.

**Proposed change.** Move the `<TestingPlatformCommandLineArguments>--ignore-exit-code 8</…>` property into the four csproj files of the empty projects, and remove it from each of them once they get tests (the existing comment G-19 already asks for that).

**Benefit.** The safety net stays for the five projects that hold all 1063 tests.
**Risk: Low.** It is a build property move. Verify with `dotnet test --solution SmartHal.slnx`.
**Principle.** Fail loudly. Checked against KISS: four lines instead of one, which is acceptable for the protection it buys.

---

### C-1 — One home for "resolve a channel profile and check it" (R14)

**Location:** `src/SmartHal.Core/Validation/DeviceRules.cs:58-73` and `src/SmartHal.Core/Validation/SchemaRules.cs:209-227`

**Finding.** Both places carry the same knowledge of rule R14: if a profile reference is present, resolve it. If it cannot be resolved, report `unresolved_type` at `<path>.profile`. If it resolves and the capability list exists, call `ProfileRules.Check` with the capability types. They differ in only two points: the projection (`TypeRef` of the device capability vs. `Type` of the template) and the `ProfilesUnknown` guard. That guard is always `false` for the device path (`judgeMissingLists: true`), so it may be applied in both places.

**Proposed change.** Add one method to `ProfileRules`, for example
`CheckReference(TypeRef? profileRef, IReadOnlyList<TypeRef>? capabilityTypes, string path, ValidationContext context, CatalogIndex index)`,
where `capabilityTypes == null` means "the list is missing, do not judge". Both callers delegate to it.

**Benefit.** R14 lives in one place. A future change, such as a new diagnostic for profile mismatch, is made once.
**Risk: Low.** The code is internal. `DeviceRulesTests`, `DeviceCatalogValidationTests` and `SchemaRulesTests` cover both paths, including the null cases (`NullHandlingTests`).
**Principle.** DRY (the same rule, not a coincidental shape). Checked against coincidental similarity: both blocks change when R14 changes, so they belong together.

---

### C-2 — Use `TypeRef` as the catalog key instead of the `(string Name, int Major)` tuple

**Location:** `src/SmartHal.Core/Validation/ReferenceGraph.cs` (18 occurrences of the tuple type, the helper `Key(TypeRef)` at `:260`), `src/SmartHal.Core/Validation/CatalogIndex.cs:24-27,104-133` (6), `src/SmartHal.Core/Validation/CatalogRules.cs:54-74` (2)

**Finding.** **FACT:** `TypeRef` is a `readonly record struct TypeRef(string Name, int Major)` (`src/SmartHal.Contracts/Primitives/TypeRef.cs:18`). It has exactly the value equality of the tuple, with ordinal string comparison. The validator nevertheless spells out the anonymous tuple 26 times, converts `TypeRef` → tuple (`ReferenceGraph.Key`) and tuple → `TypeRef` (`CatalogRules.cs:71`, `new TypeRef(id.Name, id.Major)`).

**Proposed change.** Replace `(string Name, int Major)` with `TypeRef` in the dictionaries, the stack and the sets, delete `ReferenceGraph.Key`, and build the key in `CatalogIndex.Build` as `new TypeRef(name(entry), version(entry).Major)`.

**Benefit.** The domain concept "type identity" has its domain name. About 30 noisy type spellings and both conversions go away, and the signatures become shorter and easier to read.
**Risk: Low.** It is a pure refactoring of internal types with no behaviour change. `ReferenceGraphRobustnessTests` and `ReferenceDepthTests` (including the expansion counter) pin the behaviour.
**Principle.** DRY / primitive obsession: the value object already exists. Rejected alternative: a `using TypeKey = (string Name, int Major);` alias would shorten the code but keep a second spelling of a concept that already has a name.

---

### S-5 — Split `ServerHost.RunAsync` into its steps

**Location:** `src/SmartHal.Server/Composition/ServerHost.cs:47-171`

**Finding.** `RunAsync` is about 125 lines long and runs all eight steps inline, with step comments as section markers. The SIGTERM and SIGINT registrations (`:129-143`) are two identical lambdas. **FACT:** `Microsoft.Extensions.Hosting` (ConsoleLifetime) registers its own `PosixSignalRegistration` handlers for these signals (the strings `HandlePosixSignal`/`_sigTermRegistration` are in the assembly, and `Application is shutting down...` shows up in the log). The custom handlers are still needed for event 1002 and the second-SIGINT abort, but that is worth a one-line comment.

**Proposed change** (together with S-1, because it touches the same method):
- `BuildHost(...)` → host and logger, or an exit code (S-1)
- `StartAsync(host, logger, ct)` → exit code or `null`
- `RegisterShutdownSignals(ShutdownSignalHandler)` → `IDisposable` (one loop over `[SIGTERM, SIGINT]` instead of two lambdas)
- `StopAsync(host, logger)` → step 8

`RunAsync` then reads as the sequence of section 6.5.

**Benefit.** One level of abstraction per method. Each step can be read and changed on its own, and the duplicated lambda goes away.
**Risk: Low.** Integration tests (`HarnessTests`, `ShutdownTests`, `ReadinessTests`, `LoggingTests`) cover the sequence end to end.
**Principle.** Extract Method, SRP at method level. Checked against KISS: private static methods in the same class. No new type, no interface.

---

### K-1 — Pin the catalog's copies of enum knowledge, remove local duplicates

**Location:** `src/SmartHal.Core/Catalog/SystemCapabilities.cs:43,52,143`, `src/SmartHal.Core/Catalog/LimitAlarms.cs:53`

**Finding.**
1. `core.alarms` → `acknowledge` returns `EnumType(["active_unacked", "active_acked", "cleared_unacked", "cleared"])`. This is a hand-written copy of `Contracts.Runtime.AlarmState` in its snake_case JSON form. **FACT:** no test ties the two together (no reference to `AlarmState` or `active_unacked` in the Core tests).
2. `core.connectivity` writes the status enum `["online", "degraded", "offline"]` twice: once for the property `status` and once for the alarm parameter `value`.
3. A default duration is written as `XmlConvert.ToString(delay)` in `LimitAlarms` and as the literal `"PT5M"` in `Connectivity`.

**Proposed change.**
1. **Do not** derive the catalog list from the enum. A published schema type (`core.alarms@1.0`) must not change silently when the enum grows. Instead, add a test in `CatalogConsistencyTests` that compares the list with `Enum.GetValues<AlarmState>()` serialized through `ContractsJson.Options`. A new enum value then forces a deliberate minor version of `core.alarms`.
2. Add a local `private static EnumType ConnectivityStatusType()`, the same pattern as `ModeType()` in `ActuatorCapabilities`.
3. Write both durations the same way, for example `XmlConvert.ToString(TimeSpan.FromMinutes(5))`.

**Benefit.** Enum drift is caught by a test instead of by a consumer. Each piece of knowledge has one home inside the catalog.
**Risk: Very low.** The catalog output is unchanged. `CatalogDecisionTests` and `CoreCatalogValidityTests` pin it.
**Principle.** DRY, with the counter-check against versioning: derivation was rejected on purpose.

---

## Low priority

### C-3 — Give validation paths a type of their own

**Location:** all files under `src/SmartHal.Core/Validation/` (**FACT:** 102 calls of `ValidationContext.Member`/`ValidationContext.Index`), for example `SchemaRules.cs:85`, `CapabilityTypeRules.cs:45,170`, `SchemaRules.cs:110,204`

**Finding.** `ValidationContext` combines two responsibilities: collecting errors (instance state) and building paths (static string helpers). Nested paths read inside-out, for example `ValidationContext.Member(ValidationContext.Member(path, "sleepy"), "wakeInterval")`, and every rule signature carries a raw `string path`.

**Proposed change.** Add a small `readonly record struct ValidationPath(string Value)` with `Member(string)`, `Index(int)` and `ToString()`. The rules then write `path.Member("sleepy").Member("wakeInterval")`, and `ValidationContext.Add` takes a `ValidationPath`. A lighter alternative is two extension methods on `string` (`path.Member("x")`), with no new type.

**Benefit.** Paths read left to right in JSON order. `ValidationContext` keeps one responsibility, and a typo in a path segment is easier to spot.
**Risk: Low** (internal, mechanical), but the diff touches every rule file. Do it in a separate PR with no behaviour change. The existing tests assert exact paths and catch every slip.
**Principle.** SRP (`ValidationContext`) and readability. Checked against KISS: worth it only if more rules are coming. Otherwise the extension-method variant is enough.

---

### S-6 — Drop duplicate lifecycle log lines of `Microsoft.Hosting.Lifetime`

**Location:** `src/SmartHal.Server/appsettings.json:8`, `src/SmartHal.Server/Composition/ServerHost.cs`

**Finding.** **FACT** (probe, one normal start and stop): next to the server's own events 1000/1001/1002/1003, the framework logs `Application started. Press Ctrl+C to shut down.`, `Hosting environment: …`, `Content root path: …` and `Application is shutting down...`. These are the same facts without an `EventId`. `appsettings.json` explicitly raises `Microsoft.Hosting.Lifetime` to `Information`. When validation fails, `Microsoft.Extensions.Hosting` also logs `Hosting failed to start` at Error with the full exception, in addition to the structured 1103 events.

**Proposed change.** Set `ConsoleLifetimeOptions.SuppressStatusMessages = true` in `AddSmartHalHosting`, and/or set the override for `Microsoft.Hosting.Lifetime` to `Warning`. Keep `Content root path` if it is wanted for diagnosis — it would have explained S-2.

**Benefit.** The machine-readable log of section 6.6 (FR-34) is not interleaved with unstructured duplicates.
**Risk: Low.** It is a log content change only. Check `LoggingTests`, and the spec in case section 6.6 relies on these lines.
**Principle.** One home per fact, in the log.

---

### S-7 — Delete the unused `HealthStateMonitor.Current`

**Location:** `src/SmartHal.Server/Health/HealthStateMonitor.cs:44-48`

**Finding.** **FACT:** `HealthStateMonitor.Current` is referenced nowhere, neither in `src/` nor in `tests/`. The tests read `HealthStateTracker.Current` directly.

**Proposed change.** Remove the property.
**Benefit.** One public member less in a type that only needs to publish.
**Risk: Very low.** `SmartHal.Server` is an executable, not a package.
**Principle.** YAGNI.

---

### T-2 — Keep one copy of `RepositoryLocator`

**Location:** `tests/SmartHal.ArchitectureTests/Solution/RepositoryLocator.cs`, `tests/SmartHal.IntegrationTests/Hosting/RepositoryLocator.cs`

**Finding.** **FACT:** the two files are identical except for the namespace (`diff` shows only line 1).

**Proposed change.** Keep one file, for example `tests/Shared/RepositoryLocator.cs`, and include it in both projects with `<Compile Include="../Shared/RepositoryLocator.cs" Link="Shared/RepositoryLocator.cs" />` and a neutral namespace. A shared test-utilities project is not justified for 35 lines.
**Benefit.** One place to fix the solution-file lookup.
**Risk: Very low.** Check that the architecture tests on the solution layout (`SolutionLayoutTests`) accept the new folder.
**Principle.** DRY. Checked against YAGNI: the linked file was chosen over a new project.

---

### C-4 — Format the profile messages invariantly like every other rule

**Location:** `src/SmartHal.Core/Validation/ProfileRules.cs:51-61`

**Finding.** Every other rule formats numbers through `ValidationContext.Invariant(…)`, and `ContractValidator` documents that "Messages format numbers with the invariant culture". `ProfileRules.Describe` interpolates `min`, `max` and `count` with the current culture. For `int` this only matters for the minus sign in a few cultures, so the practical impact is close to zero. The inconsistency is the point. The previous review (`2026-10-02-…-uncommitted-3.md`) already reported this as a Nitpick.
**Proposed change.** Wrap the two interpolations in `ValidationContext.Invariant`.
**Risk: Very low.**
**Principle.** Consistency with the documented convention.

---

### X-1 — Decide the casing of the `BusMessage` discriminators before the first release

**Location:** `src/SmartHal.Contracts/Bus/BusMessage.cs` (`"StateChanged"`, `"EventOccurred"`, `"CommandUpdated"`)

**Finding.** Every other discriminator of the contracts is lower or camel case (`"id"`, `"boolean"`, `"snapshot"`, `"enumMap"`, `"device"`). The bus messages use PascalCase. **FACT:** this follows the specification (`docs/Formales Schema (C#).md:393`), and `Filter.Kinds` uses the same values (`:523`), so it is not an implementation error.
**Proposed change.** No code change now. Decide once, in the spec, before `SmartHal.Contracts` is first published. Afterwards the change is a breaking wire-format change.
**Risk of the change: High** after the release (contract), **Low** before it.
**Principle.** Consistency of a public contract. This is a user and spec decision, not a code-design decision.

---

## Considered and deliberately not proposed (counter-check)

| Candidate | Why it stays |
|---|---|
| `IContractValidator` with one implementation | A real seam: `Core.Abstractions` is a module boundary that consumers depend on. It is not speculative generality. |
| Defensive `IsNull` checks on non-nullable members across the validator | **FACT:** JSON cannot produce these nulls (`RespectNullableAnnotations`/`RespectRequiredConstructorParameters` are on, `ContractsJson.cs:66-67`). The validator is still a public trust boundary for objects built in code, and the behaviour is documented and tested (`NullHandlingTests`). Removing the checks would trade robustness for brevity. |
| The catalog rebuilds its object graph on every property access | Deliberate isolation: `JsonNode` defaults are mutable. It is documented and tested (`CatalogIsolationTests`). The cost is negligible. |
| Static rule classes (`DataTypeRules`, `SchemaRules`, …) | Pure functions over immutable input, tested directly without a host. Instances or interfaces would add indirection without a seam. |
| A parameter object for `(path, context, index, depth, owner)` in `DataTypeRules` | Only `DataTypeRules` threads more than four values, and only privately. C-3 removes most of the noise already. |
| The complexity of `EcmaPattern` | Essential complexity (an ECMA-262 → .NET dialect bridge), isolated in one internal class and covered by 79 test rows. |
| `ServerTelemetry` (used only by its own test) | Mandated by FR-37/FR-38 as an extension point. YAGNI does not apply to a requirement. |
| Moving data directory provisioning out of `SmartHalOptionsValidator` | Defensible (SRP), but the spec text on `DataDirectory` ties creation to validation. S-4 fixes the actual problem (timing and side effects at build time) without this move. |
| The redundant `ProjectReference`s in `SmartHal.Core.csproj`/`SmartHal.Server.csproj` (transitively available) | **UNVERIFIED** whether the topology architecture tests require the explicit references. Left alone. |

## Status of the previous review

The Minor finding of `docs/reviews/2026-10-02-feature-device-catalog-uncommitted-3.md` (two unnamed data types produce `duplicate_key` and `null_entry`) is **fixed**. **FACT:** `CatalogRules.CheckUnique` skips entries without a name (`src/SmartHal.Core/Validation/CatalogRules.cs:66`). The Nitpick on `ProfileRules` is still open and is listed here as C-4.

## How the facts were verified

- `dotnet build SmartHal.slnx` → 0 warnings / 0 errors. `dotnet test --solution SmartHal.slnx --no-build` → 1063 passed, 0 failed.
- Server probes against the Debug output, with logs redirected to a scratch directory through `SMARTHAL_Serilog__WriteTo__File__Args__path`:
  - empty configuration → exit code 2, four `ConfigurationInvalid` events (S-3);
  - broken Serilog formatter → exit code 134, unhandled `InvalidOperationException` from `ServerHost.cs:line 91` (S-1);
  - start from a different working directory → exit code 2, 0 bytes of output (S-2);
  - valid start, SIGTERM after 4 s → exit code 0. The log shows `DataDirectoryCreated` before `HostStarting` (S-4) and the `Microsoft.Hosting.Lifetime` duplicates (S-5, S-6).
- Code facts by reading the source and by `grep`/`diff` (C-2, K-1, S-7, T-2, Q-1).
