# .NET Code Review — feature/device-catalog (uncommitted)

**Date:** 2026-10-02
**Mode:** uncommitted (origin: provided)
**Detected SDK:** 10.0.0 (installed SDK used for verification probes: 10.0.401)
**Target Framework(s):** net10.0
**Version origin:** repo:Directory.Build.props
**Checklist:** review-checklist-net10.md, plus the general checklists (security, performance, architecture, code-quality)
**Tools run:** build=N · format=N · test=N (origin: provided; the caller reports `dotnet build SmartHal.slnx --no-incremental` with 0 errors / 0 warnings, `dotnet test --solution SmartHal.slnx` with 1062/1062 passed, and an app smoke check: the server became ready and shut down cleanly on SIGTERM with exit code 0)
**Report language:** English (origin: provided)
**Exclusions:** .gitignore, *.min.js, wwwroot/lib/**
**Review strategy:** chunked (auto-selected, non-interactive; the diff exceeds the gate of 2000 LOC / 50 files)
**Diff size:** 191 files, 15371 changed LOC

## Executive Summary

| Severity | Count |
|---|---|
| Critical | 0 |
| Major | 0 |
| Minor | 1 |
| Suggestion | 2 |
| Nitpick | 4 |

**Top risks:**
1. Two data type definitions without a name in one catalog get `duplicate_key` and `null_entry` at the same path `dataTypes[1].name`. The documented contract says a missing value produces one code only (CatalogRules.cs:65, introduced with the new null-name check in SchemaRules.cs:27).
2. `EcmaPattern` reads `\xHH` and `\uXXXX` inside a class as a two-character atom followed by separate digit atoms. As a result, the reversed range `[\u{1F600}-\x7F]` passes. The correct verdicts for `[\x41-\u{1F600}]` and `[\u0041-\u{1F600}]` depend on the neutral character `a` being a hex digit, although the comment says that "any ordinary character will do" (EcmaPattern.cs:40, :270).
3. Valid ECMA-262 Unicode property escapes other than the general-category short names are rejected as `invalid_pattern`, for example `\p{Script=Greek}`, `\p{Letter}` and `\p{Alphabetic}`. The new remark in ContractValidator.cs:44 says that Unicode property escapes "pass rightly".

**Overall:** The third rework fixes every open finding of the previous review. **FACT:** `EcmaPattern` accepts `[]`, `[^]`, `\u{…}` with any number of digits up to `10FFFF`, and astral ranges checked on their code points. It rejects reversed astral ranges, surrogates and code points above `10FFFF`. Each verdict was checked against Node's `RegExp` with the `u` flag. `ClosesCycle` now uses the exact resolved definition, and the expansion counter is asserted on the index of the actual run. The remaining findings are one contract inconsistency introduced by the new null-name rule, two narrow gaps in the ECMA-262 emulation, and cosmetic issues. **FACT:** no wrapped interpolated string changed behaviour or culture handling. The split messages in DeviceRules.cs:119–128 and DataTypeRules.cs:283–285 concatenate two `ValidationContext.Invariant(…)` results, and TemplateRules.cs:79–80 and :108–109 interpolate strings only. `TypeRef`, `TypeVersion` and the `c` format of `TimeSpan` all format invariantly. The single culture-dependent number formatting found is in ProfileRules (see the Nitpick), which was not touched by the line wrapping.

**Previous findings** (report `docs/reviews/2026-10-02-feature-device-catalog-uncommitted-2.md`):

| ID | Previous finding | Status | Evidence |
|---|---|---|---|
| s-10 | `RegexOptions.ECMAScript` rejects valid ECMA-262 patterns (`[]`, `\u{1F600}`) | **Fixed** | The new `EcmaPattern.ToDotNet` (src/SmartHal.Core/Validation/EcmaPattern.cs) runs before `new Regex(…, RegexOptions.ECMAScript, …)` (DataTypeRules.cs:143–160). **FACT** (probe against the Debug assemblies built 12:22:38, after the last source change at 12:22:15): `[]` becomes `(?!)`, `[^]` becomes `[\s\S]`, `\u{0000000041}` becomes `\u0041` and `\u{1F600}` becomes a surrogate pair, and all of them are valid. `\u{110000}`, `\u{D800}`, `\u{` and `\u{1F600` are rejected. `[\u{1F600}-\u{1F64F}]` and the literal and surrogate-pair-escape forms of the same range are valid, and their reversed forms are rejected. `[\u{1F600}-\u0041]`, `[\u{1F600}-\uFFFF]` and `[a\u{1F600}-z]` are rejected. **FACT** (Node 'u' flag): these verdicts match ECMA-262. Tests: EcmaPatternTests.cs (34 valid, 25 malformed and 20 rewrite rows). Two narrow gaps remain and are reported below as new Suggestions. |
| s-11 | `for` loop in `CatalogIsolationTests` | **Fixed** | `Accessor_CalledTwice_ReturnsDifferentInstances` is a theory over `AccessorNames` with one named row per accessor (CatalogIsolationTests.cs:38–64). **FACT** (grep): no test body in SmartHal.Core.UnitTests contains `if`/`for`/`foreach`/`while`. The remaining ones are in private helpers (CatalogIsolationTests.cs:159–211). |
| s-12 | Wall-clock bound and `Validate_…` names on graph tests | **User-accepted** | Not re-reported. |
| s-13 | Expansion counter asserted on a separate index | **Fixed** | The internal `ContractValidator.ValidateCatalog(catalog, observeIndex)` (ContractValidator.cs:124) passes the index of the run to the observer through `CatalogRules.Check` (CatalogRules.cs:19–23). `Validate_ChainOfTenThousandTypes_…` asserts `LevelExpansions == types - 1` on that index (ReferenceGraphRobustnessTests.cs:159–165), and so does the review-probe test (ReferenceDepthTests.cs:199–206, `== 9`). ValidateCatalogObserverTests.cs covers a single call, observation before any check, null-catalog behaviour and distinct indexes per run. |
| n-2 | Lines over 140 characters | **Fixed** | **FACT** (awk over every changed `.cs` file): the only lines over 140 characters are JSON raw-string literals in Contracts tests, which follow repo precedent and are not reported. IsoDurationConverter.cs:12–17 now fits. The re-wrap there is ragged (line 15 is short), which is cosmetic and not re-reported. |
| n-3 | `ContractValidator` remark names ECMA constructs as .NET-only | **Fixed** | ContractValidator.cs:39–45 now names atomic groups, inline options, class subtraction, `\A`/`\Z` and comments. The new closing sentence about Unicode property escapes is only partly accurate (see the Suggestion on ContractValidator.cs:44). |
| n-4 | `ClosesCycle` compared keys only | **Fixed** | `ReferenceEquals(resolved, owner)` (ReferenceGraph.cs:94–95). **FACT** (probe): `x.a@1.0 = object{self: ref(x.a@1)}` plus `x.a@1.1 = string` now reports only `duplicate_key dataTypes[1].name`. With the minors swapped, it reports `duplicate_key` plus `ref_cycle dataTypes[0].dataType.fields.self`, which is correct because the self-referencing entry is the winner. Tests: ReferenceGraphRobustnessTests.cs:344–486 (eight duplicate/cycle cases, including the same instance listed twice and a losing duplicate inside a mutual cycle). |
| new | `DataTypeDef` without a name reports `null_entry` at `name` | **Implemented** | SchemaRules.cs:27–30. Tests: SchemaRulesTests.cs:22 (standalone) and :35 (in a catalog). See the Minor finding for two unnamed entries in one catalog. |

**Scope notes:**
- `docs/*.md` outside `docs/reviews/` is the specification and was not reviewed as code. The previous reports in `docs/reviews/` are part of the diff and were not reviewed.
- Not reported as defects, because the user accepted them or they are the user's own changes: the limitation that .NET-only regex constructs pass `invalid_pattern`, the s-3 namespace cycle Primitives↔Serialization, web-default NumberHandling, one catalog entry per name+major, records without constructor guards, no host-wiring integration test, strict enums, schema deviations B1/B2/B5/B7/B10 adopted and B3/B4/B6/B8/B9 not adopted, `InternalsVisibleTo` for SmartHal.Core.UnitTests, the s-12 wall-clock bound and test names, and the blank line at `src/SmartHal.Server/Composition/ServerHost.cs:61`.
- The whole change set was walked again. The 47 files changed after the previous report were read for this review, with a focus on `EcmaPattern.cs`, `ContractValidator.cs`, `CatalogRules.cs`, `ReferenceGraph.cs`, `DataTypeRules.cs`, `SchemaRules.cs`, the line-wrapped rule files (`CapabilityTypeRules`, `DeviceRules`, `TemplateRules`, `ValueRules`), the line-wrapped Contracts files, and the new and changed tests. The tracked-file diffs (`ServerHost.cs`, `ServiceRegistration.cs`, `ProjectPropertiesTests.cs`, both csproj files, Contracts README) are unchanged since the previous review.
- **FACT** labels mark behaviour verified with a probe console program (`probe4` in the session scratchpad). It referenced the Debug assemblies of `SmartHal.Core`, `SmartHal.Contracts` and `SmartHal.Core.Abstractions` and called the internal `EcmaPattern.ToDotNet` through reflection. ECMA-262 verdicts were checked with Node's `RegExp` (with and without the `u` flag). No repository file was changed apart from this report.

## Findings — src/SmartHal.Core/Validation/CatalogRules.cs

### [Minor][Code-Quality] src/SmartHal.Core/Validation/CatalogRules.cs:65
Two unnamed data type definitions of the same major version produce `duplicate_key` and `null_entry` at the same path. This breaks the documented single-code rule for missing values.

`CheckUnique` keys the entries by `(Name, Major)` without skipping a `null` name, so a second unnamed definition collides with the first one. **FACT** (probe): a catalog with two `DataTypeDef(null, 1.0, boolean)` reports `duplicate_key dataTypes[1].name "The type '@1' occurs more than once."`, `null_entry dataTypes[0].name` and `null_entry dataTypes[1].name`. ContractValidator.cs:23–27 promises that a missing mandatory name "is reported once as NullEntry at its own path. It never produces a second code". `CatalogIndex.Build` already skips unnamed entries (CatalogIndex.cs:119), so the reference checks are consistent. Only the uniqueness check is not. The message `'@1'` also names no type. Existing tests cover a single unnamed definition only (SchemaRulesTests.cs:35).

Keep unnamed data types out of the uniqueness check, because they are already reported as `null_entry`, and add a test with two unnamed definitions. The other lists have no null-name rule yet, so they keep their current behaviour.

```csharp
var dataTypes = context.Entries(catalog.DataTypes, "dataTypes", required: false);

// An unnamed definition is reported as a null entry at its name; it identifies nothing that could repeat.
CheckUnique(
    [.. dataTypes.Where(entry => !ValidationContext.IsNull(entry.Item.Name))],
    type => (type.Name, type.Version.Major),
    "dataTypes",
    context);
dataTypes.Each("dataTypes", (type, path) => SchemaRules.Check(type, path, context, index));

// Test
[Fact]
public void Validate_CatalogWithTwoUnnamedDataTypeDefs_ReportsOnlyANullEntryAtEachName()
{
    // Arrange
    var catalog = new TypeCatalog([], [], [],
    [
        new DataTypeDef(null!, new TypeVersion(1, 0), new BooleanType()),
        new DataTypeDef(null!, new TypeVersion(1, 0), new BooleanType())
    ]);

    // Act
    var errors = _sut.Validate(catalog);

    // Assert
    errors.CodesAndPaths().Should().Equal(
        (ValidationCodes.NullEntry, "dataTypes[0].name"),
        (ValidationCodes.NullEntry, "dataTypes[1].name"));
}
```

## Findings — src/SmartHal.Core/Validation/ContractValidator.cs

### [Suggestion][Code-Quality] src/SmartHal.Core/Validation/ContractValidator.cs:44
The .NET parser rejects valid ECMA-262 Unicode property escapes other than the general-category short names, but the remark says that Unicode property escapes "pass rightly".

**FACT** (probe): `\p{L}`, `\p{Lu}`, `\P{L}` and `^[\p{L}\p{N}]+$` are valid. `\p{Script=Greek}`, `\p{sc=Grek}`, `\p{Letter}`, `\p{General_Category=Letter}`, `\p{Alphabetic}`, `\p{ASCII}` and `\p{Emoji}` are reported as `invalid_pattern`. **FACT** (Node, `u` flag): `\p{Script=Greek}` and `\p{Letter}` are valid ECMA-262. In the other direction, `\p{IsGreek}` (a .NET block name) passes, which falls under the accepted .NET-only limitation. A vendor schema that restricts a name to one script, for example `^\p{Script=Latin}+$`, therefore fails the catalog. The impact is low, because such patterns are rare in device schemas, but the remark states the opposite. At minimum, correct the remark. Optionally, `EcmaPattern` can hand any `\p{…}`/`\P{…}` that matches the ECMA-262 property grammar to the parser as a neutral `\p{L}`, the same way it neutralizes astral code points, so that only malformed property syntax is rejected.

```csharp
/// … the anchors <c>\A</c> and <c>\Z</c> and comments <c>(?#…)</c>. Lookbehind passes rightly. Of the Unicode
/// property escapes only the general categories in their short form, such as <c>\p{L}</c> or <c>\p{Lu}</c>, are
/// known to the .NET parser; script, binary and long-form properties such as <c>\p{Script=Greek}</c>,
/// <c>\p{Letter}</c> or <c>\p{Alphabetic}</c> are valid ECMA-262 but reported as invalid patterns.
```

## Findings — src/SmartHal.Core/Validation/EcmaPattern.cs

### [Suggestion][Code-Quality] src/SmartHal.Core/Validation/EcmaPattern.cs:270
Inside a class, `\xHH` and `\uXXXX` (when they are not a surrogate pair) are read as a two-character atom, and their digits as separate atoms. As a result, a reversed range against a `\xHH` endpoint passes, and two correct verdicts depend on the neutral character being a hex digit.

The fallback reads `\x` or `\u` as a two-character atom. `EscapedCodePoint` assigns `\u` its value, but returns `null` for `\x`, because `x` is a letter (line 297). The remaining hex digits become separate literal atoms, so the last digit, not the escape, becomes the range endpoint. **FACT** (probe):
- `[\u{1F600}-\x7F]` is rewritten to `[a-\x7F]` and reported as valid. **FACT** (Node, `u` flag): it is a SyntaxError (the range is reversed). The `<returns>` doc at lines 50–52 promises `null` for this case.
- `[\x41-\u{1F600}]` is rewritten to `[\x4a]` and `[\u0041-\u{1F600}]` to `[\u004a]`. Both are valid only because `a` is a hex digit. **FACT** (probe): `[\x4x]` and `[\u004x]` are rejected by .NET. The comment at line 40, "any ordinary character will do", is therefore wrong: changing `NeutralCharacter` to a non-hex letter would report valid patterns as `invalid_pattern`.

`\0` and `\cX` endpoints happen to give the right verdict today, because `a` (U+0061) is above both. Read the fixed-length escapes as whole atoms with their code point, correct the comment, and add test rows for the cases above.

```csharp
// TryReadEscapeAtom, after the surrogate-pair branch
if (TryReadUnicodeEscape(pattern, position, out var bmpUnit))
{
    // \uXXXX as a whole, so its digits are never read as atoms of their own.
    atom = new ClassAtom(pattern.Substring(position, 6), bmpUnit, IsHyphen: false);

    return true;
}

if (TryReadHexEscape(pattern, position, out var hexUnit))
{
    // \xHH as a whole, for the same reason.
    atom = new ClassAtom(pattern.Substring(position, 4), hexUnit, IsHyphen: false);

    return true;
}

// Reads \xHH with exactly two hexadecimal digits.
private static bool TryReadHexEscape(string pattern, int position, out int unit)
{
    unit = 0;

    if (position + 4 > pattern.Length || pattern[position + 1] != 'x'
        || HexValue(pattern[position + 2]) is var high and < 0 || HexValue(pattern[position + 3]) is var low and < 0)
    {
        return false;
    }

    unit = (high * 16) + low;

    return true;
}

// Line 40: the neutral character must keep the parser's verdict on everything around it.
private const char NeutralCharacter = 'a';

// EcmaPatternTests: Validate_MalformedPattern_… gains @"[\u{1F600}-\x7F]";
// Validate_PatternValidInEcmaScript_… gains @"[\x41-\u{1F600}]" and @"[\u0041-\u{1F600}]".
```

### [Nitpick][Code-Quality] src/SmartHal.Core/Validation/EcmaPattern.cs:263
The comment begins with a literal emoji (bytes `F0 9F 98 80`), not with the escape sequence the branch reads. That makes it unclear what "is one code point".

The branch handles a surrogate pair written as two `\uXXXX` escapes. The literal character probably comes from an editor or tool that decoded the escape. Spell out the escape, which C# does not interpret in a comment.

```csharp
// A surrogate pair written as two escapes, such as \uD83D\uDE00, is one code point in the Unicode mode.
```

## Findings — src/SmartHal.Core/Validation/ProfileRules.cs

### [Nitpick][Code-Quality] src/SmartHal.Core/Validation/ProfileRules.cs:61
The `profile_violation` messages interpolate `min`, `max` and `count` with the current culture. ContractValidator.cs:48 promises that messages "format numbers with the invariant culture".

**FACT** (probe): under `sv-SE` the negative sign is U+2212, and the message template at line 61 renders `at most −1 times`. A negative `max` reaches line 61 whenever the channel has the capability. In the catalog run, `SchemaRules.CheckCounts` reports the negative count but does not stop `ProfileRules` (SchemaRules.cs:226; this reachability was reasoned from the code, not probed end to end). Non-negative values format the same in every culture, so the impact is cosmetic. This file was not touched by the line wrapping. Wrap both messages like the others.

```csharp
if (expected.Min is { } min && count < min)
{
    return ValidationContext.Invariant(
        $"requires the capability '{expected.Type}' at least {min} times, but it occurs {count} times.");
}

if (expected.Max is { } max && count > max)
{
    return ValidationContext.Invariant(
        $"allows the capability '{expected.Type}' at most {max} times, but it occurs {count} times.");
}
```

## Findings — src/SmartHal.Core/SmartHal.Core.csproj

### [Nitpick][Code-Quality] src/SmartHal.Core/SmartHal.Core.csproj:11
The comment gives the reference-graph counter as the only reason for `InternalsVisibleTo`. The tests now also use the internal `EcmaPattern.ToDotNet` and `ContractValidator.ValidateCatalog`.

The `InternalsVisibleTo` itself is user-approved and is not questioned here. Only the stated reason is out of date: EcmaPatternTests.cs:135 and ValidateCatalogObserverTests.cs use internals that are unrelated to the counter.

```xml
<!--
  The unit tests reach internals that have no public surface: the expansion counter of the reference graph (the
  deterministic proof that shared data types are expanded once), the observer of a catalog run, and the ECMA-262
  pattern rewrite.
-->
<InternalsVisibleTo Include="SmartHal.Core.UnitTests" />
```

## Findings — tests/SmartHal.Core.UnitTests/Validation/EcmaPatternTests.cs

### [Nitpick][Tests] tests/SmartHal.Core.UnitTests/Validation/EcmaPatternTests.cs:48
The row `[^]]` is asserted under `Validate_PatternValidInEcmaScript_ReportsNoViolation`. In the Unicode mode that `EcmaPattern` documents (lines 12–13), this pattern is a syntax error.

**FACT** (Node): `new RegExp("[^]]", "u")` throws a SyntaxError (a lone `]` is not a pattern character with the `u` flag), and only the non-Unicode mode accepts it. The pass itself falls under the accepted direction: the .NET parser takes a lone `]` as a literal. The test name, however, records it as ECMA-valid, so a later stricter check would look like a regression. Move the row to a theory that documents the accepted limitation.

```csharp
// The .NET parser accepts these, although the Unicode mode of ECMA-262 rejects them; accepted limitation.
[Theory]
[InlineData("[^]]")]
[InlineData("[a]]")]
public void Validate_UnicodeModeSyntaxErrorTheDotNetParserAccepts_ReportsNoViolation(string pattern)
{
    // Arrange
    var definition = Definition(pattern);

    // Act
    var errors = _sut.Validate(definition);

    // Assert
    errors.Should().BeEmpty();
}
```

## Tool Output Appendix

### dotnet build
- Not run by the reviewer (build=no, provided). The caller reports `dotnet build SmartHal.slnx --no-incremental`: 0 errors, 0 warnings.

### dotnet test
- Not run by the reviewer (test=no, provided). The caller reports `dotnet test --solution SmartHal.slnx`: 1062/1062 passed. The caller also reports an app smoke check: the server reported ready and shut down cleanly on SIGTERM with exit code 0.

### dotnet format
- Skipped (format=no, provided).

### Reviewer tooling notes
- As in the previous runs, `scripts/collect-diff.sh` lists `"docs/Formales Schema (C#).md\t"` with a raw tab character in its JSON. This is an issue in the review script, not in the repository.
- The verification probe was a console program in the session scratchpad (`probe4`, Release build). It checked the pattern rewrite and its verdicts, Unicode property escapes, unnamed duplicate definitions, the duplicate/cycle edge of n-4, the dependence on the neutral character, and `sv-SE` number formatting. ECMA-262 verdicts came from Node's `RegExp`. No repository file was modified apart from this report.
