# .NET Code Review — feature/device-catalog (uncommitted)

**Date:** 2026-10-02
**Mode:** uncommitted (origin: provided)
**Detected SDK:** 10.0.0 (installed SDK used for verification probes: 10.0.401)
**Target Framework(s):** net10.0
**Version origin:** repo:Directory.Build.props
**Checklist:** review-checklist-net10.md, plus the general checklists (security, performance, architecture, code-quality)
**Tools run:** build=N · format=N · test=N (origin: provided; the caller reports `dotnet build SmartHal.slnx --no-incremental` with 0 errors / 0 warnings, `dotnet test --solution SmartHal.slnx` with 957/957 passed, and an app smoke check: the server became ready and shut down cleanly on SIGTERM with exit code 0)
**Report language:** English (origin: provided)
**Exclusions:** .gitignore, *.min.js, wwwroot/lib/**
**Review strategy:** chunked (auto-selected, non-interactive; the diff exceeds the gate of 2000 LOC / 50 files)
**Diff size:** 187 files, 14200 changed LOC

## Executive Summary

| Severity | Count |
|---|---|
| Critical | 0 |
| Major | 0 |
| Minor | 0 |
| Suggestion | 4 |
| Nitpick | 3 |

**Top risks:**
1. `RegexOptions.ECMAScript` rejects some patterns that are valid ECMA-262, for example `[]` and `\u{1F600}`, so a valid vendor catalog can fail with `invalid_pattern` (DataTypeRules.cs:135). This is the reverse of the limitation the user accepted, and it is low in impact.
2. Two regression tests are named `Validate_…` but measure a separate `CatalogIndex` instead of the validation run, and one test still uses a wall-clock bound (ReferenceDepthTests.cs:158/190, ReferenceGraphRobustnessTests.cs:149). The linearity of `Validate` itself is not asserted deterministically.
3. The `ContractValidator` remarks give lookbehind and Unicode categories as examples of ".NET-only constructs". Both are ECMA-262 (ContractValidator.cs:39–40).

**Overall:** The second rework fixes all six open findings of the previous review. The new `ReferenceGraph` (Tarjan SCC plus post-order memoization, both iterative) is correct and linear. **FACT:** 3,000 random catalogs with cycles agree with an independent oracle, and a chain of 100,000 data types validates in 433 ms without a stack overflow. The validation report from the last review took 14.1 s and now takes 10 ms. No Critical, Major or Minor issue remains. The remaining findings concern test precision, one documentation inaccuracy, and narrow edge cases.

**Previous findings** (report `docs/reviews/2026-10-02-feature-device-catalog-uncommitted.md`):

| ID | Previous finding | Status | Evidence |
|---|---|---|---|
| M-3 | Reference-depth check has exponential cost | **Fixed** | `ReferenceGraph` (src/SmartHal.Core/Validation/ReferenceGraph.cs) builds the edges once per run, computes the strongly connected components with an iterative Tarjan (`Connect`, line 179), and caches the struct levels in post-order with an explicit stack (`Levels`, line 90). `CatalogIndex.References` (CatalogIndex.cs:95) creates it lazily once per `CatalogIndex`, and `DataTypeRules.CheckReference` (line 215) uses it. **FACT** (probe, Release build of the probe against the Debug assemblies built 11:21, after the last source change): the probe catalog of the last review (10 × 8) takes 10 ms (previously 14,105 ms). 2,000 × 8 takes 39 ms, 20,000 × 4 takes 269 ms, a 100,000-type chain takes 433 ms, and a 100,000-type cycle takes 153 ms, with no stack overflow. **FACT** (probe): 3,000 random catalogs of 1–8 types with random cycles, arrays and nested structs give exactly the `ref_cycle`/`struct_too_deep` code and path sequence of an independent reachability/memo oracle (0 mismatches). **FACT** (code + test): a `RefType` without a name is reported once as `null_entry` at `…ref`, with or without a catalog (DataTypeRules.cs:225, `Validate_ReferenceWithoutName_ReportsNullEntryAtTheReference`, `Validate_CapabilityPropertyReferenceWithoutName_ReportsNullEntry`). The 10,000-type chain is covered by `Validate_ChainOfTenThousandTypes_CompletesAndExpandsEveryTypeOnce`. The expansion counter `LevelExpansions` is asserted in four tests. |
| m-4 | Duration pattern accepts a trailing `\n` and non-ASCII digits | **Fixed** | The pattern is now `\AP…\z` with `[0-9]` (IsoDurationConverter.cs:68). **FACT** (probe): `"PT30S\n"` and `"PT٣S"` are rejected with the pattern message, and `"PT30S"` and `"P1DT2H"` are read. Tests: `InlineData` for `PT30S\n`, an Arabic-Indic digit and a full-width digit (ContractReadingEdgeTests.cs:221–223). |
| s-6 | Strict enum converter takes over every enum | **Fixed** | `CanConvert` is limited to enums of the SmartHal.Contracts assembly (StrictEnumConverterFactory.cs:25–26), and this is documented in the remarks. **FACT** (probe): with `ContractsJson.Options`, a foreign enum with an alias (`B = 0, Alias = 0`) round-trips without an exception, and `[JsonStringEnumMemberName("dark-red")]` is honoured. Tests: ContractReadingEdgeTests.cs:282–369 (foreign enum, alias, the consumer's own `JsonStringEnumConverter`, an undeclared foreign value, a foreign dictionary key). |
| s-7 | `ref_cycle` reported at references outside the cycle | **Fixed** | `ReferenceGraph.ClosesCycle` reports only when the owner and the target share a component (ReferenceGraph.cs:80), and `CheckReference` passes the owning definition (DataTypeRules.cs:252). The `ValidationCodes.RefCycle` doc (line 81) and the `ContractValidator` remarks (lines 29–35) match. **FACT** (oracle probe above, plus tests `Validate_PointingTypeListedAfterTheCycle_ReportsTheCycleMembersOnly`, `Validate_TwoCyclesJoinedByOneWayReference_ReportsNoCycleAtTheJoin`, `Validate_ThreeTypeCycleWithOutsideReference_…`, `Validate_CapabilityPropertyReferringToACyclicType_ReportsNoCycleAtTheProperty`). See the Nitpick on ReferenceGraph.cs:80 for an edge case with duplicate entries. |
| s-8 | `invalid_pattern` uses the .NET dialect and a localized message | **Fixed** (the limitation that .NET constructs pass is user-accepted and not re-reported) | `new Regex(pattern, RegexOptions.ECMAScript, …)` with a fixed message (DataTypeRules.cs:135–143). Test: `Validate_InvalidPattern_ReportsAStableMessageWithoutExceptionText`. Two new, separate observations follow: valid ECMA-262 patterns that are rejected (Suggestion) and inaccurate examples in the remark (Nitpick). |
| s-9 | `if` in a test body (`ValueRulesTests`) | **Fixed** | The test is split into `Validate_SleepyWakeIntervalNotPositive_ReportsInvalidDuration` (theory) and `Validate_SleepyWakeIntervalPositive_ReportsNoViolation` (fact), ValueRulesTests.cs:150–174. **FACT** (grep over all new test files): no test body contains `if`. The remaining `if`/`foreach` statements are in private helpers. One `for` loop in a test body remains in CatalogIsolationTests.cs:56, which predates both reworks (see the Suggestion). |
| — | `InternalsVisibleTo` for SmartHal.Core.UnitTests | **Accepted** (deliberate) | src/SmartHal.Core/SmartHal.Core.csproj:14 is the only `InternalsVisibleTo` in the repository, and it targets a test project, which the architecture checklist allows. A comment states the reason. |

**Scope notes:**
- `docs/*.md` outside `docs/reviews/` is the specification and was not reviewed as code. The previous reports in `docs/reviews/` are part of the diff and were not reviewed.
- Not reported as defects, because the user accepted them or they are the user's own changes: the s-3 namespace cycle Primitives↔Serialization, web-default NumberHandling, one catalog entry per name+major, records without constructor guards, no host-wiring integration test, strict enums, schema deviations B1/B2/B5/B7/B10 adopted and B3/B4/B6/B8/B9 not adopted, the ECMAScript-mode limitation of s-8, and the blank line at `src/SmartHal.Server/Composition/ServerHost.cs:61`.
- The whole change set was walked again. The files changed after the previous review were read in full: `ReferenceGraph.cs`, `CatalogIndex.cs`, `DataTypeRules.cs`, `ContractValidator.cs`, `SchemaRules.cs`, `ValidationCodes.cs`, `IsoDurationConverter.cs`, `StrictEnumConverterFactory.cs`, `SmartHal.Core.csproj` and the four test files. The tracked-file diffs (`ServerHost.cs`, `ServiceRegistration.cs`, `ProjectPropertiesTests.cs`, both csproj files) are unchanged since the previous review and raise nothing new.
- VERSIONING.md classification is unchanged: the package has not been released, so the stricter duration pattern is not breaking.
- **FACT** labels mark behaviour verified with a probe console program in the session scratchpad. It referenced the Debug assemblies of `SmartHal.Contracts`, `SmartHal.Core`, `SmartHal.Core.Abstractions` and `SmartHal.Adapter.Sdk` (built 2026-10-02 11:21:36) and ran on .NET SDK 10.0.401. No repository file was changed apart from this report.

## Findings — src/SmartHal.Core/Validation/DataTypeRules.cs

### [Suggestion][Code-Quality] src/SmartHal.Core/Validation/DataTypeRules.cs:135
The ECMAScript mode of .NET rejects some patterns that are valid ECMA-262, so a valid JSON Schema pattern can be reported as `invalid_pattern`.

This is the reverse of the limitation the user accepted. The user accepted that .NET-only syntax passes. Here, valid ECMA syntax fails. **FACT** (probe, `new Regex(p, RegexOptions.ECMAScript, 100 ms)`): `[]` (an empty class, which never matches in ECMAScript) and `\u{1F600}` (a code-point escape, valid with the `u` flag and as an identity escape without it) throw `RegexParseException`. Unverified: whether JSON Schema mandates the `u` flag. The impact is low, because both constructs are rare in device schemas. The cheapest remedy is to document it next to the accepted limitation, so that adapter authors know which pattern subset the kernel accepts.

```csharp
/// <para>
/// Patterns are compiled in the ECMAScript mode of .NET. That mode rejects malformed expressions, still accepts some
/// .NET-only constructs such as atomic groups <c>(?&gt;…)</c> or inline options <c>(?i)</c>, and rejects a few
/// ECMA-262 forms such as the empty class <c>[]</c> and code-point escapes <c>\u{…}</c>; use <c>[^\s\S]</c> and
/// surrogate pairs or literal characters instead.
/// </para>
```

## Findings — src/SmartHal.Core/Validation/ContractValidator.cs

### [Nitpick][Code-Quality] src/SmartHal.Core/Validation/ContractValidator.cs:39
The remark names lookbehind and Unicode categories as ".NET-only constructs", but both are ECMA-262.

Lookbehind is ES2018, and `\p{…}` is valid with the `u` flag. The test comment at ReferenceGraphRobustnessTests.cs:327–329 also says that lookbehind is valid ECMA-262, so the remark contradicts its own test. **FACT** (probe): constructs that really are .NET-only and pass in ECMAScript mode include `(?>a)`, `(?i)a`, `[a-z-[aeiou]]`, `\A`, `\Z` and `(?#c)`. Use those as the examples (see the snippet in the previous finding).

```csharp
/// Patterns are compiled in the ECMAScript mode of .NET, the dialect of JSON Schema. That mode rejects malformed
/// expressions but still accepts some .NET-only constructs such as atomic groups <c>(?&gt;…)</c>, inline options
/// <c>(?i)</c> or character class subtraction <c>[a-z-[aeiou]]</c>.
```

## Findings — src/SmartHal.Core/Validation/ReferenceGraph.cs

### [Nitpick][Code-Quality] src/SmartHal.Core/Validation/ReferenceGraph.cs:80
`ClosesCycle` compares keys (name, major) only. A duplicate entry that loses the minor-version tie-break can therefore get a spurious `ref_cycle`.

**FACT** (probe): the catalog `x.a@1.0 = object { self: ref(x.a@1) }` together with `x.a@1.1 = string` reports `duplicate_key dataTypes[1].name` and also `ref_cycle dataTypes[0].dataType.fields.self`. The reference resolves to `x.a@1.1`, a string, so it closes no cycle. The pre-rework check followed the resolved definition and would not have reported this. The impact is minimal, because the catalog already fails with `duplicate_key` (one entry per name+major is the accepted rule). Pass an owner only when the definition is the one the index resolves to.

```csharp
// DataTypeRules.CheckDefinition
TypeRef? owner = ValidationContext.IsNull(definition.Name) ? null : new TypeRef(definition.Name, definition.Version.Major);

// A duplicate that lost the minor-version tie-break is not a node of the reference graph.
if (owner is { } key && index is not null && !ReferenceEquals(index.FindDataType(key), definition))
{
    owner = null;
}
```

## Findings — src/SmartHal.Contracts/Serialization/IsoDurationConverter.cs

### [Nitpick][Code-Quality] src/SmartHal.Contracts/Serialization/IsoDurationConverter.cs:14
The inserted sentence about fractional seconds made the remark line 172 characters long. `.editorconfig` sets `max_line_length = 140`, and the surrounding lines wrap at about 120.

```csharp
/// <remarks>
/// Only exact, non-negative durations in days, hours, minutes and seconds are accepted, for example <c>"P90D"</c>,
/// <c>"PT1H30M"</c> or <c>"PT0.5S"</c>; only seconds may carry a fraction, of at most seven digits. Years and months
/// (<c>"P1Y"</c>, <c>"P1M"</c>) are rejected because they have no fixed length and <c>"P1M"</c> is easily mistaken
/// for <c>"PT1M"</c>; negative durations are rejected in both directions.
/// </remarks>
```

## Findings — tests/SmartHal.Core.UnitTests/Catalog/CatalogIsolationTests.cs

### [Suggestion][Tests] tests/SmartHal.Core.UnitTests/Catalog/CatalogIsolationTests.cs:56
`GroupProperties_AccessedTwice_ReturnDifferentInstances` asserts inside a `for` loop over a flat array of pairs. The checklist flags any test body with `if`/`for`. This test predates both reworks and was not flagged before.

A failure names only the loop index, and the flat `pairs` array depends on its own odd/even convention. A theory with one row per accessor gives one named test case per accessor.

```csharp
public static TheoryData<string, Func<object>> Accessors => new()
{
    { "SystemCapabilities.Battery", () => SystemCapabilities.Battery },
    { "ActuatorCapabilities.Color", () => ActuatorCapabilities.Color },
    // …
    { "CoreCapabilityCatalog.ToTypeCatalog", () => CoreCapabilityCatalog.ToTypeCatalog() }
};

[Theory]
[MemberData(nameof(Accessors))]
public void GroupProperty_AccessedTwice_ReturnsDifferentInstances(string name, Func<object> access)
{
    // Arrange

    // Act
    var first = access();
    var second = access();

    // Assert
    second.Should().NotBeSameAs(first, $"{name} must build a new instance per access");
}
```

## Findings — tests/SmartHal.Core.UnitTests/Validation/ReferenceDepthTests.cs

### [Suggestion][Tests] tests/SmartHal.Core.UnitTests/Validation/ReferenceDepthTests.cs:158
`Validate_ChainOfTypesWithEightReferencesEach_ExpandsEveryTypeOnce` and `Validate_LongChainOfTypesWithTwoReferencesEach_ExpandsEveryTypeOnce` (line 175) never call `Validate`. They call `ReferenceGraph.Levels` on their own `CatalogIndex`. `Validate_ProbeCatalogOfTheReview_ReportsEveryTooDeepReferenceQuickly` (line 190) asserts a wall-clock bound of 1 s.

Under `Method_Condition_Expected` the names promise something about `Validate` that the tests do not check. The stopwatch assertion depends on the machine and is redundant now that the deterministic `LevelExpansions` counter exists. It measured 10 ms in a Release probe, so it is unlikely to flake, but it does not prove the bound. Rename the two graph tests after the method they exercise, and drop the time assertion in favour of the error count, or of the counter (see the next finding).

```csharp
[Fact]
public void Levels_ChainOfTypesWithEightReferencesEach_ExpandsEveryTypeOnce()
{
    // … unchanged …
}

[Fact]
public void Validate_ProbeCatalogOfTheReview_ReportsEveryTooDeepReference()
{
    // Arrange
    var catalog = DataTypesOnly(Chain(types: 10, referencesPerType: 8));

    // Act
    var errors = _sut.Validate(catalog);

    // Assert
    errors.Should().HaveCount(64).And.OnlyContain(error => error.Code == ValidationCodes.StructTooDeep);
}
```

## Findings — tests/SmartHal.Core.UnitTests/Validation/ReferenceGraphRobustnessTests.cs

### [Suggestion][Tests] tests/SmartHal.Core.UnitTests/Validation/ReferenceGraphRobustnessTests.cs:149
`Validate_ChainOfTenThousandTypes_CompletesAndExpandsEveryTypeOnce` asserts `LevelExpansions` on a second `CatalogIndex` that the test builds itself, not on the index that `_sut.Validate(catalog)` used.

The test therefore proves that the graph is linear when it is queried once from the head. It does not prove that the validation run, which calls `Levels` once per `RefType` in every definition and capability, expands each type only once. **FACT** (probe): the validation run is linear today (a 100,000-type chain takes 433 ms). A future change that, for example, creates a `CatalogIndex` per rule would not be caught, because the test passes on time alone. Assert the counter of the index the run used. One option is an internal `CatalogRules.Check` overload or test seam that returns the index. Another is to validate through `CatalogRules` directly with an index created in the test.

```csharp
// Arrange
const int types = 10_000;
var catalog = DataTypesOnly(Chain(types));
var index = new CatalogIndex(catalog, judgeMissingLists: false);
var context = new ValidationContext();

// Act: the same rules Validate(TypeCatalog) runs, with an index the test can inspect.
// CatalogRules.Check(TypeCatalog, ValidationContext, CatalogIndex) is a NEW internal overload; today Check creates its
// own index (CatalogRules.cs:21), and the existing Check(catalog, context) would delegate to it.
CatalogRules.Check(catalog, context, index);

// Assert
context.Errors.Should().HaveCount(types - 2).And.OnlyContain(error => error.Code == ValidationCodes.StructTooDeep);
index.References.LevelExpansions.Should().Be(types);
```

## Tool Output Appendix

### dotnet build
- Not run by the reviewer (build=no, provided). The caller reports `dotnet build SmartHal.slnx --no-incremental`: 0 errors, 0 warnings.

### dotnet test
- Not run by the reviewer (test=no, provided). The caller reports `dotnet test --solution SmartHal.slnx`: 957/957 passed. The caller also reports an app smoke check: the server reported ready and shut down cleanly on SIGTERM with exit code 0.

### dotnet format
- Skipped (format=no, provided).

### Reviewer tooling notes
- As in the previous runs, `scripts/collect-diff.sh` lists `"docs/Formales Schema (C#).md\t"` with a raw tab character in its JSON. This is an issue in the review script, not in the repository, and the diff content was not affected.
- The verification probe was a console program in the session scratchpad (`probe3`). It checked the regex dialect, scaling, a random-catalog oracle, the duration edge cases, a foreign enum with an alias and `JsonStringEnumMemberName`, and the duplicate-entry edge. No repository file was modified apart from this report.
