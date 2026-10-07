# Code-Design Refactorings — smarthal-kernel

**Date:** 2026-10-03
**Branch:** `feature/device-catalog` (HEAD `cd19623`; the only uncommitted file is `docs/reviews/2026-10-02-improvement-proposals.md`)
**Scope:** production code in `src/` (~8,100 LOC, 7 projects), plus duplicated test infrastructure in `tests/`
**Method:** a review against the `code-design` skill. Every refactoring names the principle it rests on (SRP, SoC, DRY, Extract Method/Class, Dependency Inversion) **and** the counter-principle it was checked against (KISS, YAGNI, coincidental similarity, Speculative Generality, Middle Man).
**Baseline (FACT, run 2026-10-03):** `dotnet build SmartHal.slnx` → 0 warnings, 0 errors. `dotnet test --solution SmartHal.slnx --no-build` → 1063 of 1063 passed.

Only this report was added to the repository. Statements marked **FACT** were checked against the source, by `grep`/`diff`, or by running the build. Statements marked **UNVERIFIED** are hypotheses.

**Relation to the previous report.** `docs/reviews/2026-10-02-improvement-proposals.md` (uncommitted) used the same method. This report covers **refactorings only**: changes to structure with no change in behaviour. I re-checked the refactoring items of that report against the source (C-1, C-2, C-3, S-5, S-7, K-1, T-2). They hold, and they appear here again with sharper moves. I also added new findings (R-2, R-4, R-6, R-9, R-10, R-11). Items from that report that change behaviour or process (S-1, S-2, S-3, S-4, S-6, Q-1, T-1, C-4, X-1) are not refactorings. They are listed in [Behaviour changes that stay in the previous report](#behaviour-changes-that-stay-in-the-previous-report) and are not repeated.

---

## Summary

| ID | Refactoring | Area | Move(s) | Principle | Priority | Effort | User decision |
|---|---|---|---|---|---|---|---|
| R-1 | Use `TypeRef` as the type identity in the validator | Core | Replace primitive tuple with value object | DRY / primitive obsession | High | S | Optional (Contracts helper) |
| R-2 | Give the "unresolved reference" report one home, outside `SchemaRules` | Core | Move Function, Consolidate Duplicate | DRY, SRP | High | S | — |
| R-3 | One home for the R14 profile check | Core | Consolidate Duplicate | DRY | Medium | S | — |
| R-4 | Extract `DeviceTypeRules` from `SchemaRules`; split `Check(DeviceType)` into its parts | Core | Extract Class, Extract Method | SRP | Medium | S | — |
| R-5 | Give validation paths a type of their own | Core | Value object | SRP, readability | Low | M | — |
| R-6 | One home for the "mandatory collection is null" violation | Core | Consolidate Duplicate | DRY | Low | XS | — |
| R-7 | Split `ServerHost.RunAsync` into its steps | Server | Extract Method | SRP at method level | Medium | S | — |
| R-8 | Return the external configuration file from `ConfigurationStack.Apply` instead of passing it through a property bag | Server | Replace hidden hand-off with return value | KISS | Medium | XS | Yes (public signature) |
| R-9 | Keep the options failure format and its parser in one type | Server | Move Function | DRY | Medium | XS | Depends on S-3 |
| R-10 | Separate the data directory I/O from the option rules | Server | Split Phase | SoC | Low | S | Yes (spec) |
| R-11 | Delete the unused `HealthStateMonitor.Current` | Server | Remove dead code | YAGNI | Low | XS | — |
| R-12 | Remove local duplicates of catalog knowledge and pin the `AlarmState` copy | Catalog | Extract local function, characterization test | DRY | Medium | S | — |
| R-13 | Keep one copy of `RepositoryLocator` | Tests | Consolidate Duplicate | DRY | Low | XS | — |

Effort: XS < 30 min, S < half a day, M ≈ 1 day.

**Recommended slicing into PRs** (each one is behaviour-neutral and has to pass the 1063 tests unchanged):

1. **Core PR:** R-1 → R-2 → R-3 → R-4 → R-6, in this order (each step makes the next smaller). R-5 goes in a separate PR because it touches every rule file.
2. **Server PR:** R-7, R-8, R-9, R-11. Do R-7 *before* S-1 of the previous report, so the behaviour change lands in a method that is already split. R-10 goes together with S-4 of the previous report.
3. **Catalog/tests PR:** R-12, R-13.

---

## Test coverage — why these moves are safe

`code-design` allows moving code only when tests cover every path that moves. **FACT:**

- **Core validation.** The rule classes are `internal static` and are reached through the public `ContractValidator` (14 test files construct `new ContractValidator()`) and through the internal `ContractValidator.ValidateCatalog` (`InternalsVisibleTo SmartHal.Core.UnitTests`, `src/SmartHal.Core/SmartHal.Core.csproj:14`). The tests assert exact paths, codes, and messages. That makes them characterization tests for every refactoring in R-1 to R-6: a moved path that changes a message or a path turns a test red. Relevant suites: `DeviceRulesTests`, `SchemaRulesTests`, `DeviceCatalogValidationTests`, `CatalogValidationTests`, `NullHandlingTests`, `ReferenceGraphRobustnessTests`, `ReferenceDepthTests`, `ValueRulesTests`, `ValidateCatalogObserverTests`.
- **Server.** `ConfigurationStackTests` (5 test methods on `Apply`), `OptionsFailureTests` (including a round trip over real validator output, `OptionsFailureTests.cs:65-74`), `SmartHalOptionsValidatorTests`, `HealthStateMonitorTests`, and the integration suites `HarnessTests`, `ShutdownTests`, `ReadinessTests`, `LoggingTests` (whole start and stop sequence).
- **Catalog.** `CatalogDecisionTests`, `CoreCatalogValidityTests`, `CatalogConsistencyTests` pin the catalog output.

No path was found that would need new characterization tests before it moves. The one gap is R-12: no test ties the catalog's copy of `AlarmState` to the enum. Closing that gap is part of R-12.

---

## Core — validation (`src/SmartHal.Core/Validation`)

### R-1 — Use `TypeRef` as the type identity in the validator

**Location:** `ReferenceGraph.cs:29-40, 52, 92, 117, 195-260` (18 spellings of `(string Name, int Major)`), `CatalogIndex.cs:24-27, 104-133` (6), `CatalogRules.cs:26-38, 56-71` (2), `CoreDataTypes.cs:32`

**Finding (FACT).** `TypeRef` is `readonly record struct TypeRef(string Name, int Major)` (`src/SmartHal.Contracts/Primitives/TypeRef.cs:18`). Its value equality is exactly the tuple's. Even so, the validator spells out the anonymous tuple 26 times and converts in both directions: `ReferenceGraph.Key(TypeRef)` (`:260`) turns a `TypeRef` into a tuple, and `CatalogRules.cs:71` turns a tuple back into a `TypeRef` (`new TypeRef(id.Name, id.Major)`) just to print it.

The rule "the identity of a schema type is its name plus its **major** version" is written out in seven places:
`CatalogIndex.Build` (`:124`), the four `CheckUnique` lambdas in `CatalogRules.Check` (`:26, 30, 34, 38`), `ReferenceGraph.ClosesCycle` (`:92`), and `CoreDataTypes.ReferenceTo` (`:32`).

**Refactoring.**
1. Replace `(string Name, int Major)` with `TypeRef` in all dictionaries, sets, stacks, and signatures. Delete `ReferenceGraph.Key`.
2. Give the identity rule one home in the validator: `internal static TypeRef IdentityOf(string name, TypeVersion version) => new(name, version.Major);` on `CatalogIndex`. `CatalogIndex.Build`, `CatalogRules.CheckUnique` (the `identity` parameter becomes `Func<T, TypeRef>`), and `ReferenceGraph.ClosesCycle` use it.

**Principle.** DRY: the value object already exists, and the identity rule gets one home.
**Counter-check.** A `using TypeKey = (string Name, int Major);` alias was rejected. It shortens the code but keeps a second name for a concept that already has one. An `ISchemaType { Name; Version; }` interface on the four contract records would be cleaner, but it changes the public contract (see the user decision below). `CoreDataTypes.ReferenceTo` (in `Core.Catalog`) stays as it is, so the catalog does not depend on the validator's namespace.
**Risk:** low. Internal types only. `ReferenceGraphRobustnessTests` asserts the expansion counter, so a changed traversal order would show up.
**User decision (optional):** should `TypeRef` get a public factory `TypeRef.Of(string name, TypeVersion version)` in `SmartHal.Contracts`? Then `CoreDataTypes` could use it as well. Default: no public API change, internal helper only.

---

### R-2 — Give the "unresolved reference" report one home, outside `SchemaRules`

**Location:** `SchemaRules.cs:182-183` (`ReportUnresolved`), its callers `DeviceRules.cs:67, 99, 160` and `SchemaRules.cs:54, 170, 201, 220`, and the inline copy in `DataTypeRules.cs:254-261`. The "judged and missing capability type" check is repeated in `SchemaRules.cs:52-55, 166-171, 197-206`.

**Finding (FACT).**
- `ReportUnresolved` is a helper that all rule classes need, but it lives in `SchemaRules`. As a result, `DeviceRules` calls into a sibling rule class three times to report its own violations. That is a misplaced function: it does not belong to the schema rules.
- `DataTypeRules.CheckReference` builds the same violation by hand: code `UnresolvedType` with the message `"The data type '{ref}' is not in the type catalog."`. That is exactly what `ReportUnresolved(ref, "data type", …)` produces. The message format of `unresolved_type` therefore has two homes.
- The pattern `if (index.IsUnresolvedCapability(x)) ReportUnresolved(x, "capability type", path, context);` appears three times in `SchemaRules`.

**Refactoring.** A new `internal static class ReferenceRules` in the same folder, with:
- `ReportUnresolved(TypeRef reference, string kind, string path, ValidationContext context)`, moved from `SchemaRules`;
- `CheckCapability(TypeRef reference, string path, ValidationContext context, CatalogIndex index)`, which wraps the `IsUnresolvedCapability` check and the report.

`DeviceRules`, `SchemaRules`, and `DataTypeRules` call it. `DataTypeRules.cs:256-259` becomes `ReferenceRules.ReportUnresolved(refType.Ref, "data type", ValidationContext.Member(path, "ref"), context)`.

**Principle.** DRY (one message format per code) and SRP (`SchemaRules` loses a responsibility that is not its own).
**Counter-check.** This is not a Middle Man: the unit owns the code, the message, and the judging rule (`CapabilitiesUnknown`). Making it a method on `ValidationContext` was rejected, because that class already has two responsibilities (see R-5). `CatalogIndex` was rejected too: it resolves references and should not report.
**Risk:** low. The messages stay byte-identical. The tests on `unresolved_type` in `DeviceCatalogValidationTests`, `CatalogValidationTests`, and `ReferenceDepthTests` would catch any change.

---

### R-3 — One home for the R14 profile check

**Location:** `DeviceRules.cs:58-73` and `SchemaRules.cs:209-227`

**Finding (FACT).** Both blocks encode the same rule: if a channel names a profile, resolve it. If it cannot be resolved, report it at `<path>.profile`. If it resolves and the capability list exists, call `ProfileRules.Check` with the capability types. There are only two differences:
1. The projection: `Capability.TypeRef` for a device versus `CapabilityTemplate.Type` for a template.
2. The `ProfilesUnknown` guard. It exists only in `SchemaRules`, but it is always `false` on the device path, because `ContractValidator.Validate(Device, TypeCatalog)` builds its index with `judgeMissingLists: true` (`ContractValidator.cs:110`, `CatalogIndex.cs:41`). So the guard may be applied on both paths.

**Refactoring.** Add `ProfileRules.CheckReference(TypeRef? profileRef, IReadOnlyList<TypeRef>? capabilityTypes, string path, ValidationContext context, CatalogIndex index)`, where `capabilityTypes == null` means "the list is missing, do not judge". It reports through `ReferenceRules.ReportUnresolved` (R-2). Both callers shrink to one line each.

**Principle.** DRY. The two blocks share knowledge, not just shape.
**Counter-check (coincidental similarity).** A change to R14, such as a new diagnostic for an optional capability, changes both blocks at once, so they belong together. `ProfileRules` is the existing home of R14 (its summary says so), so no new unit is needed.
**Risk:** low. The null cases are pinned by `NullHandlingTests`.
**Note:** this is C-1 of the previous report, adjusted to build on R-2.

---

### R-4 — Extract `DeviceTypeRules` from `SchemaRules`; split `Check(DeviceType)` into its parts

**Location:** `SchemaRules.cs:66-116` (`Check(DeviceType)`), `:185-228` (`CheckChannelTemplate`)

**Finding (FACT).**
- The class summary has to say "the schema types **besides** capability types". The class has four public groups that share no state and change for different reasons: data type definitions (a one-line delegation to `DataTypeRules`), channel profiles (R7), device types (R8, R11, R14), and migrations (R10). On top of that it holds the shared helper from R-2. This matches the SRP signal "public members fall into unrelated groups".
- The device type group is the largest one (about 100 of 249 lines, `:59-116` and `:185-228`). `Check(DeviceType)` alone is 50 lines and works at three levels in one body: channel keys, the sleepy configuration, and binding templates with three nested loops (templates → parameters/mappings → poll interval, `:92-115`).
- The sibling classes follow the pattern of one class per contract type: `CapabilityTypeRules`, `DeviceRules`, `TemplateRules`. A device type has no class of its own.

**Refactoring.**
1. **Extract Method** inside `Check(DeviceType)`: `CheckChannels(…)`, `CheckSleepy(…)`, `CheckBindingTemplates(…)`. After that, `Check(DeviceType)` reads as three calls.
2. **Extract Class** `internal static class DeviceTypeRules` with `Check(DeviceType, …)` and the private helpers `CheckChannelTemplate` and `CheckBindingTemplates`. `CatalogRules.cs:39` and `ContractValidator.cs:82` call `DeviceTypeRules.Check` instead.

`SchemaRules` keeps the data type, profile, and migration checks (about 150 lines including doc comments).

**Principle.** SRP (one reason to change per unit) and Extract Method (one level of abstraction per function).
**Counter-check (KISS).** No interface and no instance; the static style of the siblings is kept. The remaining small groups of `SchemaRules` are deliberately **not** split further. Moving `Check(ChannelProfile)` (R7) into `ProfileRules` would be defensible, but the two rules (R7 checks the profile itself, R14 checks a channel against a profile) change for different reasons. So that move is not proposed.
**Risk:** low. `SchemaRulesTests` and `DeviceCatalogValidationTests` go through `ContractValidator` and the catalog, so they are independent of the class layout.

---

### R-5 — Give validation paths a type of their own

**Location:** all rule files. **FACT:** 95 lines in `src/SmartHal.Core/Validation` call `ValidationContext.Member`/`ValidationContext.Index`, and 7 of them nest the calls, for example `ValidationContext.Member(ValidationContext.Member(path, "sleepy"), "wakeInterval")` (`SchemaRules.cs:85`) and `CapabilityTypeRules.cs:45, 170`.

**Finding.** `ValidationContext` has two responsibilities: it collects violations (instance state) and it builds paths (static string helpers). Nested paths read inside out, and every rule signature carries a raw `string path`.

**Refactoring.** Add `internal readonly record struct ValidationPath(string Value)` with `Member(string)`, `Index(int)`, and `ToString()`. The rules then write `path.Member("sleepy").Member("wakeInterval")`, and `ValidationContext.Add` takes a `ValidationPath`. A lighter alternative is two extension methods on `string` (`path.Member("x")`). That variant has no new type but still reads left to right.

**Principle.** SRP (`ValidationContext`) and readability.
**Counter-check (KISS).** The diff touches every rule file, mechanically. It pays off only if the validator keeps growing; if it does not, the extension method variant is enough. Do it in its own PR after R-1 to R-4, so the diffs do not mix.
**Risk:** low. The tests assert exact paths.

---

### R-6 — One home for the "mandatory collection is null" violation

**Location:** `ValueRules.cs:50-54` and `ValidationContext.cs:149-155`

**Finding (FACT).** `ValueRules.CheckHistoryPolicy` builds `NullEntry` with the text `"The mandatory collection is null."` by hand. The private `ValidationContext.ReportMissingCollection` builds the same violation. It cannot be reused here because `Entries<T>` requires `where T : class`, and `Rollup.Aggregates` is an `IReadOnlyList<RollupAggregate>` of an enum (`src/SmartHal.Contracts/Schema/Rollup.cs`, `RollupAggregate.cs`).

**Refactoring.** Make the helper callable: `public void AddMissingCollection(string path)` on `ValidationContext` (used by `ReportMissingCollection`), and call it from `ValueRules`.

**Principle.** DRY: one text per violation, as the class remarks of `ContractValidator` require ("reported once as `NullEntry`").
**Counter-check.** Relaxing the `class` constraint of `Entries<T>` was rejected. It would change a generic signature with 30+ callers to save a single site.
**Risk:** very low.

---

## Server (`src/SmartHal.Server`)

### R-7 — Split `ServerHost.RunAsync` into its steps

**Location:** `Composition/ServerHost.cs:47-171`

**Finding (FACT).** `RunAsync` is 125 lines long. It runs steps 1 to 8 of the start sequence inline, with `// Step n` comments as section markers. The SIGTERM and SIGINT registrations (`:129-143`) are two identical lambdas.

**Refactoring.** Private static methods in the same class:
- `TryBuildConfiguration(args)` → builder and external file, or `ExitCodes.InvalidConfiguration` (step 1);
- `ComposeServices(builder, configure)` (steps 2 and 3);
- `StartAsync(host, logger, ct)` → exit code or `null` (steps 4 and 5);
- `RegisterShutdownSignals(ShutdownSignalHandler)` → `IDisposable`, with one loop over `[PosixSignal.SIGTERM, PosixSignal.SIGINT]` (step 7);
- `StopAsync(host, logger)` (step 8).

`RunAsync` then reads as the sequence of section 6.5.

**Principle.** Extract Method: one level of abstraction per method, and the duplicated lambda goes away.
**Counter-check (KISS).** No new type, no interface, no strategy per step: the steps run in one fixed order. When S-1 of the previous report (handling errors from `builder.Build()`) comes later, it changes only `ComposeServices`/`BuildHost`.
**Risk:** low. The integration tests cover the sequence end to end, including exit codes, the log order, and the second SIGINT.

---

### R-8 — Return the external configuration file from `ConfigurationStack.Apply` instead of passing it through a property bag

**Location:** `Composition/ConfigurationStack.cs:40, 63-66, 106-115`, `Composition/ServerHost.cs:62-63`

**Finding (FACT).** `Apply` resolves the external file and puts it into `((IHostApplicationBuilder)builder).Properties["SmartHal.ExternalConfigurationFile"]`. `ServerHost` reads it back right after the call, through `ExternalConfigurationFileOf`: a cast, a `TryGetValue`, a type test, and a fallback (`NotConfigured`) that cannot occur in the real flow. `ExternalConfigurationFileOf` has exactly one caller (`ServerHost.cs:63`). The value is computed by one method and wanted by the very next statement, so a hidden hand-off through a string-keyed bag is indirection with no gain.

**Refactoring.** `Apply` returns the `ExternalConfigurationFile` it resolved. Delete `ExternalConfigurationFilePropertyKey`, `ExternalConfigurationFileOf`, and the property write. `ServerHost` uses `var externalFile = ConfigurationStack.Apply(builder);`.

**Principle.** KISS: data flows as a return value instead of through shared state.
**Counter-check.** Existing test calls such as `ConfigurationStack.Apply(builder);` still compile when the return value is ignored (**FACT:** `ConfigurationStackTests.cs:72, 102, 117, 140`).
**User decision:** `Apply` is `public` and `ExternalConfigurationFile` is `internal`. A public method cannot return an internal type, so either (a) make the record `public`, which is recommended because `SmartHal.Server` is an executable and not a package, or (b) make `Apply` `internal` and add `InternalsVisibleTo SmartHal.Server.UnitTests`. **FACT:** `SmartHal.Server.csproj` has no `InternalsVisibleTo` today.
**Risk:** very low.

---

### R-9 — Keep the options failure format and its parser in one type

**Location:** `Configuration/SmartHalOptionsValidator.cs:199-202` (`Failure`) and `Configuration/OptionsFailure.cs:68-72, 90-93` (`CustomMessage`)

**Finding (FACT).** The validator writes every violation as the text `"{Section}:{Field}: {reason}"`. `OptionsFailure` parses exactly this text back with a regex. The format is one piece of knowledge, but it lives in two classes. If one side changes, the other breaks silently; only the round-trip test `OptionsFailureTests.cs:65-74` would notice.

**Refactoring.** `OptionsFailure` owns both directions: `public string ToMessage() => $"{Section}:{Field}: {Reason}";` next to `Parse`. The validator writes `failures.Add(new OptionsFailure(SmartHalOptions.SectionName, field, reason).ToMessage());` and drops its `Failure` helper.

**Principle.** DRY: the format and its parser sit side by side.
**Counter-check.** The text round trip itself stays, because `IValidateOptions` transports only strings. That is a framework constraint, not a design choice. A structured side channel would be more code for no behaviour gain.
**Dependency:** if S-3 of the previous report removes the DataAnnotations layer, `OptionsFailure.Parse` shrinks to the custom branch. Do R-9 after the S-3 decision, so that the type is not changed twice.
**Risk:** very low.

---

### R-10 — Separate the data directory I/O from the option rules

**Location:** `Configuration/SmartHalOptionsValidator.cs:115-197`

**Finding (FACT).** The validator holds two kinds of work. The first is pure rules: instance name set and at most 64 characters, shutdown timeout greater than 0 and at most 5 minutes. The second is file system I/O with side effects: `Path.GetFullPath`, `Directory.CreateDirectory`, a probe file write, and a log event (`DataDirectoryCreated`). The I/O part is 80 of the 200 lines, and it is also why the validator needs a logger. The pure rules do not need one.

**Refactoring (Split Phase).** Add `internal static class DataDirectory` with `Prepare(string path) → DataDirectoryState` (`InvalidPath`, `CreateFailed`, `NotWritable`, `Ready`, plus a flag whether it was created and the full path). The validator maps the state to its failure text and logs the creation. The point in time stays the same (still inside validation), so the behaviour does not change.

**Principle.** SoC: decide (the failure text) apart from act (the file system).
**Counter-check (KISS/YAGNI).** No interface for the file system: the existing tests run against real temporary directories, and no test needs a fake. Without an interface the gain is readability and a unit that can be tested without options. That is modest, so the priority is low.
**User decision:** the spec text on `SmartHalOptions.DataDirectory` ("created when it does not exist") ties creation to validation. Should provisioning leave validation entirely, for example into a hosted start step? That is a behaviour change and belongs together with S-4 of the previous report. Default: only the split phase above, with the point in time unchanged.
**Risk:** low.

---

### R-11 — Delete the unused `HealthStateMonitor.Current`

**Location:** `Health/HealthStateMonitor.cs:48`

**Finding (FACT).** `HealthStateMonitor.Current` is referenced nowhere in `src/` or `tests/`. `HealthStateMonitorTests` checks the log events, and `HealthStateTrackerTests` reads `HealthStateTracker.Current` directly.
**Refactoring.** Remove the property.
**Principle.** YAGNI. **Counter-check:** `SmartHal.Server` is an executable, so no external consumer exists.
**Risk:** very low.

---

## Catalog (`src/SmartHal.Core/Catalog`)

### R-12 — Remove local duplicates of catalog knowledge and pin the `AlarmState` copy

**Location:** `SystemCapabilities.cs:43, 52, 53, 143`, `LimitAlarms.cs:53`, `ActuatorCapabilities.cs:133`

**Finding (FACT).**
1. `core.connectivity` spells out the status enum `["online", "degraded", "offline"]` twice: once for the property `status` (`:43`) and once for the alarm parameter `value` (`:52`). The two must stay equal, or the alarm refers to a value the property cannot take. `ActuatorCapabilities.ModeType()` (`:133`) shows the pattern the codebase already uses for this.
2. A default duration is written as `XmlConvert.ToString(delay)` in `LimitAlarms.cs:53`, but as the literal `"PT5M"` in `SystemCapabilities.cs:53`.
3. `core.alarms` → `acknowledge` returns `EnumType(["active_unacked", "active_acked", "cleared_unacked", "cleared"])` (`:143`). That is a hand-written copy of `Contracts.Runtime.AlarmState` in its snake_case wire form. No test ties the two together (`grep` for `AlarmState`/`active_unacked` in `tests/SmartHal.Core.UnitTests` finds nothing).

**Refactoring.**
1. Add `private static EnumType ConnectivityStatusType()` next to `DeviceKeyPayload()`, used at both sites.
2. Write the 5 minutes as `XmlConvert.ToString(TimeSpan.FromMinutes(5))`, the same way `LimitAlarms` does.
3. **Do not** derive the list from the enum. A published schema type (`core.alarms@1.0`) must not change silently when the enum grows. Instead, add a characterization test to `CatalogConsistencyTests` that compares the list with `Enum.GetValues<AlarmState>()` serialized through `ContractsJson.Options`. A new enum value then forces a deliberate new minor version of `core.alarms`.

**Principle.** DRY inside the catalog. For item 3, the counter-check against versioning decides: duplication with a pinning test, not derivation.
**Risk:** very low. The catalog output stays byte-identical; `CatalogDecisionTests` and `CoreCatalogValidityTests` prove it.

---

## Tests

### R-13 — Keep one copy of `RepositoryLocator`

**Location:** `tests/SmartHal.ArchitectureTests/Solution/RepositoryLocator.cs`, `tests/SmartHal.IntegrationTests/Hosting/RepositoryLocator.cs`

**Finding (FACT).** `diff` shows that the two files differ only in line 1 (the namespace).
**Refactoring.** Keep one file, for example `tests/Shared/RepositoryLocator.cs` with a neutral namespace. Link it into both projects with `<Compile Include="../Shared/RepositoryLocator.cs" Link="Shared/RepositoryLocator.cs" />`.
**Principle.** DRY. **Counter-check (YAGNI):** a shared test utilities project is not justified for one 35-line class; a linked file is enough. Check that `SolutionLayoutTests` accepts the new folder.
**Risk:** very low.

---

## Considered and deliberately left alone (counter-check)

| Candidate | Why it stays |
|---|---|
| `TypeRefConverter` and `TypeVersionConverter` are almost identical (`Read`: token check + `TryParse`; `Write`: round-trip guard) | Two occurrences, and each has its own message text and format hint. A generic `ParsableStringConverter<T> where T : IParsable<T>` would need the texts as parameters. DRY comes last: wait for the third value object. |
| Three "duplicate in a list" loops (`KeyRules.CheckUnique`, `CatalogRules.CheckUnique`, `DataTypeRules.CheckEnum`) | The shared part is three lines of `HashSet.Add`. The code, path, message, and null semantics differ at each site. A generic helper would save no knowledge. |
| `CapabilityTypeRules.CheckName` vs. `CheckNames` | Same shape by coincidence: a single nullable name with an alarm-specific message versus a list with index paths. |
| The seven `ContractValidator.Validate` overloads (each `ThrowIfNull` + `Run`) | Seven public one-liners, one per contract type. A generic dispatch would be harder to read (KISS veto). |
| `IContractValidator` with one implementation | A real seam: `Core.Abstractions` is a module boundary that consumers depend on. |
| `ICoreApi` without any implementation | A public API contract from the specification. YAGNI does not apply to a contract that is known to come. |
| `ReferenceGraph` combines Tarjan's SCC and the level measurement (~260 lines) | Both work on the same component numbering, so the class is cohesive. A generic SCC class would have one user (Speculative Generality). R-1 removes most of its noise. |
| `EcmaPattern` (395 lines) | Essential complexity (an ECMA-262 → .NET bridge), isolated in one internal class and covered by 80 `InlineData` rows in `EcmaPatternTests`. |
| Static rule classes without interfaces | Pure functions over immutable input, tested directly. An interface would add indirection without a seam. |
| A parameter object for `(path, context, index, depth, owner)` in `DataTypeRules` | Only private methods of one class carry it. R-5 removes most of the noise. |
| `ServerTelemetry` (only used by its own test) | Required by FR-37/FR-38 as an extension point. |
| `HealthState.cs` contains both the enum `HealthState` and the class `HealthStateTracker` | Only the file layout is affected, so it is a style issue and outside the scope of `code-design`. A candidate for the `refactor` skill or a style pass. |
| `CoreDataTypes.ReferenceTo` repeats the identity rule of R-1 | It sits in `Core.Catalog`, and the catalog should not depend on the validator. It can only be removed by the optional public `TypeRef.Of` (R-1, user decision). |
| `ProjectReference`s in `SmartHal.Core.csproj`/`SmartHal.Server.csproj` that are also available transitively | **UNVERIFIED** whether the topology architecture tests require the explicit references. This is a build question, not a code-design question. |

---

## Behaviour changes that stay in the previous report

These items in `docs/reviews/2026-10-02-improvement-proposals.md` change behaviour, error handling, or process. They are not refactorings, so they are not repeated here. Their interaction with this report:

| Item | Interaction |
|---|---|
| S-1 (exit code for errors from `builder.Build()`) | Do it after R-7, in the extracted method. |
| S-2 (content root / working directory) | Independent. |
| S-3 (drop the DataAnnotations layer) | Decide before R-9. |
| S-4 (`ShutdownTimeoutConfigurator` triggers validation during `Build()`) | Together with R-10. |
| S-6, Q-1, T-1, C-4, X-1 | Independent. |

---

## Decisions for the user

1. **R-1:** add a public factory `TypeRef.Of(string name, TypeVersion version)` to `SmartHal.Contracts`? Recommended: **no** for now. The internal helper is enough, and the public API stays untouched.
2. **R-8:** make `ExternalConfigurationFile` `public` (recommended), or make `ConfigurationStack.Apply` `internal` and add `InternalsVisibleTo`?
3. **R-10:** should data directory provisioning leave the options validation, which would be a spec change? Recommended: decide together with S-4. Until then, only the split phase.
4. **R-9:** waits for the S-3 decision.

---

## How the facts were verified

- `dotnet build SmartHal.slnx` → 0 warnings, 0 errors. `dotnet test --solution SmartHal.slnx --no-build` → 1063 of 1063 passed (2026-10-03, HEAD `cd19623`).
- The source of every file in `src/SmartHal.Core/Validation`, `src/SmartHal.Server`, `src/SmartHal.Contracts/Serialization`, `src/SmartHal.Contracts/Primitives/TypeRef.cs`/`TypeVersion.cs`, and the catalog infrastructure (`CatalogDefinitions`, `CoreCapabilityCatalog`, `CoreDataTypes`, `HistoryPolicies`, `LimitAlarms`, `Units`) was read in full.
- Counts and references by `grep`: tuple spellings per file (R-1), callers of `ReportUnresolved`/`UnresolvedType` (R-2), path helper lines (R-5), the duplicated message text (R-6), callers of `ExternalConfigurationFileOf`/`Apply` (R-8), references to `HealthStateMonitor.Current` (R-11), and `AlarmState` in the Core tests (R-12).
- `diff` of the two `RepositoryLocator.cs` files (R-13).
- The code-graph index (`tokensave`) was not used for these facts. It serves the `main` branch, because `feature/device-catalog` is not tracked, so every fact was read from the working tree.
