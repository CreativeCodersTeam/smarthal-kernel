# .NET Code Review — feature/device-catalog (uncommitted)

**Date:** 2026-10-02
**Mode:** uncommitted (origin: provided)
**Detected SDK:** 10.0.0 (installed SDK used for verification probes: 10.0.401)
**Target Framework(s):** net10.0
**Version origin:** repo:Directory.Build.props
**Checklist:** review-checklist-net10.md, plus the general checklists (security, performance, architecture, code-quality)
**Tools run:** build=N · format=N · test=N (origin: provided; the caller reports `dotnet build SmartHal.slnx --no-incremental` with 0 errors / 0 warnings, `dotnet test --solution SmartHal.slnx` with 911/911 passed, and an app smoke check: the server started, became ready and shut down cleanly on SIGTERM with exit code 0)
**Report language:** English (origin: provided)
**Exclusions:** .gitignore, *.min.js, wwwroot/lib/**
**Review strategy:** chunked (auto-selected, non-interactive; the diff exceeds the gate of 2000 LOC / 50 files)
**Diff size:** 183 files, 13069 changed LOC

## Executive Summary

| Severity | Count |
|---|---|
| Critical | 0 |
| Major | 1 |
| Minor | 1 |
| Suggestion | 4 |
| Nitpick | 0 |

**Top risks:**
1. The new reference-depth logic walks referenced data types without memoization or cutoff, so its cost grows exponentially with the fan-out of a catalog. A 3.2 KB catalog of ten data types with eight reference fields each takes about 14 s to validate (DataTypeRules.cs:228). Vendor catalogs can come in through adapters, so this is a denial-of-service vector.
2. The ISO duration pattern ends in `$` and uses `\d`, so `"PT30S\n"` is accepted. The contract promises "only exact durations", and tightening this after release counts as breaking under VERSIONING.md (IsoDurationConverter.cs:67).
3. The strict enum converter is registered for every enum in the shared options. A consumer enum with alias values fails with a permanent `TypeInitializationException`, and `[JsonStringEnumMemberName]` is ignored without a warning (StrictEnumConverterFactory.cs:19).

**Overall:** The rework fixes all ten previous findings that were open. Probes against the built assemblies confirm each fix (see below), and the new behaviour is covered by tests. One new Major finding came with the M-2 fix: the depth calculation through references is correct, but it has no complexity bound. It should be memoized before the validator is applied to catalogs from outside the kernel. The other new findings are small strictness and diagnostics issues.

**Previous findings** (report `docs/reviews/2026-09-30-feature-device-catalog-uncommitted.md`):

| ID | Previous finding | Status | Evidence |
|---|---|---|---|
| M-1 | Enum converter accepts comma lists and C# member names | **Fixed** | `StrictEnumConverterFactory` maps exact snake_case names only, for values and for dictionary keys (`ReadAsPropertyName`/`WriteAsPropertyName`). **FACT** (probe): `"sent, acked"`, `"Sent"`, `"SENT"` and `1` are rejected with `JsonException`, and `"sent"` is read. The dictionary key `"Sent"` is rejected and `"sent"` is read. Writing `(CommandStatus)42` throws `JsonException`. Tests: `Deserialize_EnumNotGivenAsItsExactSnakeCaseName_ThrowsJsonException`, `Deserialize_EnumDictionaryKeyNotInSnakeCase_ThrowsJsonException`, `Serialize_UndeclaredEnumValue_ThrowsJsonException`. |
| M-2 | R1 does not follow `RefType`; cycles undetected | **Fixed** (new complexity issue, see the Major finding below) | `CatalogIndex.FindDataType` keeps the definitions, and `DataTypeRules.CheckReference` adds the struct levels of the target and reports `ref_cycle` (`ValidationCodes.RefCycle`). **FACT** (probe): `x.cyc = object { self: ref(x.cyc) }` and `x.arr = array(ref(x.arr))` both report `ref_cycle`. `ReferenceDepthTests` covers depth through refs, arrays, mutual cycles, unresolved refs and the case without a catalog. |
| m-1 | Duplicate properties / keys accepted | **Fixed** | `AllowDuplicateProperties = false` (ContractsJson.cs:64). **FACT** (probe): `{"a":1,"a":2}` throws `JsonException`. Tests cover duplicates that differ only in case and duplicate dictionary keys. |
| m-2 | Missing discriminator throws `NotSupportedException` | **Fixed** | A `TypeInfoResolver` modifier (`RejectMissingDiscriminator`) sets `CreateObject` of every abstract object type to throw `JsonException`. **FACT** (probe): `Deserialize<DataType>("{\"values\":[\"a\"]}")` throws `JsonException: A DataType needs its type discriminator 'type' to be read.` Tests cover nested types, bus messages, substitutions, transform steps, and abstract types without polymorphism. No test asserts `NotSupportedException` for reading any more. |
| m-3 | Validation messages use the current culture | **Fixed** | `ValidationContext.Invariant` is used for every message with a floating-point or possibly negative number. **FACT** (probe, `de-DE`): "The minimum 1.5 is greater than the maximum 0.5." Test: `Validate_UnderGermanCulture_FormatsNumbersInMessagesInvariantly`. A few integer counts in `ProfileRules`/`KeyRules` are still interpolated directly. They are never negative, so their text does not depend on the culture, and this is not reported. |
| s-1 | Missing value checks | **Fixed** | New codes `invalid_pattern` and `invalid_duration`, `invalid_range` for negative `maxLength`/`maxItems`, non-finite bounds and deadband values, and positive timeouts, wake intervals, poll intervals, retentions and rollup intervals (`ValueRules`). **FACT** (probe): `StringType(-1, "(")` reports `invalid_range` and `invalid_pattern`. |
| s-2 | Reference equality of records undocumented | **Fixed** | A script check confirmed that every record in `SmartHal.Contracts` with a list, dictionary or `JsonNode` member carries the "Equality compares … by reference" remark, and that no record without one has the remark. |
| s-3 | `Primitives` ↔ `Serialization` namespace cycle | **Accepted by the user** (not re-reported) | — |
| s-4 | `XmlConvert` accepts calendar and negative durations | **Fixed**, with one residual gap (see the Minor finding below) | `IsoDurationConverter` checks a strict pattern before `XmlConvert`. **FACT** (probe): `P1M`, `P1DT`, `-PT5S`, `PT1.12345678S` and overflow are rejected with `JsonException`. `TimeSpan.MaxValue` round-trips, and writing a negative value throws. |
| s-5 | Conditional logic in `TryParse` tests | **Fixed** | `TypeRefTests`/`TypeVersionTests` no longer contain `if` in a test body. The same pattern came back in a new test (see the Suggestion on `ValueRulesTests.cs:163`). |
| n-1 | `HsvRef` repeats name and major of `Hsv` | **Fixed** | `HsvRef => ReferenceTo(Hsv)` derives both values (CoreDataTypes.cs:35). |

**Scope notes:**
- `docs/IoT-Domänenmodell.md` and `docs/Formales Schema (C#).md` are the specification and were not reviewed as code. The previous report in `docs/reviews/` is part of the diff and was not reviewed.
- Not reported as defects, because the user accepted them or they are the user's own changes: web-default NumberHandling, one catalog entry per name+major, records without constructor guards, no host-wiring integration test, strict enums for unknown names, schema deviations B1/B2/B5/B7/B10 adopted and B3/B4/B6/B8/B9 not adopted, the namespace cycle s-3, and the blank line at `src/SmartHal.Server/Composition/ServerHost.cs:61`.
- VERSIONING.md classification is unchanged. All public types in `SmartHal.Contracts` are new and the package has not been released, so the JSON-strictness changes of the rework are *not breaking*. The new validation codes in `SmartHal.Core.Abstractions` are additive.
- The **FACT** labels mark behaviour verified with probe console programs. The probes referenced the freshly built Debug assemblies of `SmartHal.Contracts`, `SmartHal.Core` and `SmartHal.Core.Abstractions` (built 2026-10-02 10:54, after the last source change) and ran on .NET SDK 10.0.401 in the session scratchpad. No repository file was changed apart from this report.

## Findings — src/SmartHal.Core/Validation/DataTypeRules.cs

### [Major][Performance] src/SmartHal.Core/Validation/DataTypeRules.cs:228
The struct-level walk through references has no memoization or cutoff, so validating a small catalog takes exponential time.

`CheckReference` (line 208) calls `StructLevels` for every `RefType` it meets. `StructLevels`/`FieldLevels`/`ReferenceLevels` expand the whole reference graph again each time. The `visiting` set only detects cycles and does not prevent re-expanding shared (diamond) sub-graphs. `FieldLevels` visits every field even after the depth limit is exceeded, so a chain of data types whose fields refer to the next type costs *k*ⁿ steps. **FACT** (probe, Release build of the probe against the Debug assemblies):

| Data types (n) × reference fields each (k) | Catalog JSON | `Validate(TypeCatalog)` |
|---|---|---|
| 8 × 8 | ~2.5 KB | 342 ms |
| 10 × 8 | ~3.2 KB | 14,105 ms |
| 22 × 2 | ~3 KB | 774 ms |
| 24 × 2 | ~3.4 KB | 3,158 ms |

Each additional data type multiplies the time by *k*. The domain model expects vendor types to arrive through adapters, and the catalog validation is the gate for them. A small, malformed or hostile catalog can therefore hold a validation thread for minutes (OWASP A04 Insecure Design: uncontrolled resource consumption). The `CatalogIndex` is built once per `Validate` call, so it is the natural place to cache the levels per reusable data type. Every data type is then expanded at most once per call, and the walk becomes linear in the size of the catalog. Add a regression test with a wide, deep reference chain that asserts the result. Optionally, also check the visit count or a generous time bound.

```csharp
// CatalogIndex: one cache per validation call
private readonly Dictionary<(string Name, int Major), int?> _structLevels = [];

public bool TryGetStructLevels((string Name, int Major) key, out int? levels) => _structLevels.TryGetValue(key, out levels);

public void SetStructLevels((string Name, int Major) key, int? levels) => _structLevels[key] = levels;

// DataTypeRules
private static int? ReferenceLevels(TypeRef reference, CatalogIndex index, HashSet<(string Name, int Major)> visiting)
{
    if (index.FindDataType(reference) is not { } definition)
    {
        return 0;
    }

    var key = Key(reference);

    if (index.TryGetStructLevels(key, out var cached))
    {
        return cached;
    }

    if (!visiting.Add(key))
    {
        return null;
    }

    var levels = StructLevels(definition.DataType, index, visiting);
    visiting.Remove(key);

    // A null result means the type reaches a type that is still being expanded, which makes it part of that cycle
    // (or a referrer of it, as today); caching it keeps the behaviour and bounds the walk to one expansion per type.
    index.SetStructLevels(key, levels);

    return levels;
}

// CheckReference: start from the cache as well, so each RefType costs O(1) after the first expansion
var levels = ReferenceLevels(refType.Ref, index, []);
```

### [Suggestion][Code-Quality] src/SmartHal.Core/Validation/DataTypeRules.cs:213
`ref_cycle` is also reported at references that are not part of the cycle, which contradicts the documented meaning of the code.

`ValidationCodes.RefCycle` and the `ContractValidator` remarks define the code as "a reference … leads back to a data type it is part of". `CheckReference`, however, reports it whenever the referenced type *contains* a cycle. **FACT** (probe): a catalog with `x.outer = array(ref(x.cyc))` and `x.cyc = object { self: ref(x.cyc) }` reports `ref_cycle` at `dataTypes[0].dataType.items` (`x.outer` is not part of any cycle) as well as at `dataTypes[1].dataType.fields.self`. A capability property that refers to a cyclic vendor type gets a `ref_cycle` error of its own as well, so one defect is reported once per referrer, and the message points at the wrong type. Either report the cycle only where it closes and count a referenced cyclic type as resolved without levels, or change the documentation to "refers to a data type that is cyclic".

```csharp
// Report the cycle only at the reference that closes it; a referrer outside the cycle adds no violation of its own.
var visiting = new HashSet<(string Name, int Major)> { Key(refType.Ref) };
var levels = StructLevels(definition.DataType, index, visiting);

if (levels is null)
{
    if (ClosesCycle(refType, path, definition)) // e.g. the enclosing definition is the referenced one or on its cycle
    {
        context.Add(path, ValidationCodes.RefCycle, $"The reference to '{refType.Ref}' leads back to a data type it is part of.");
    }

    return;
}

// Or, keeping today's behaviour, adjust the documentation:
/// <summary>A reference to a reusable data type leads to a cyclic data type (R1).</summary>
public const string RefCycle = "ref_cycle";
```

### [Suggestion][Code-Quality] src/SmartHal.Core/Validation/DataTypeRules.cs:108
`invalid_pattern` checks the pattern in the .NET regex dialect, but the contract does not name a dialect, and the README calls the data types "a subset of JSON Schema".

JSON Schema `pattern` uses ECMA-262 syntax. The .NET parser accepts constructs that ECMAScript rejects or reads differently, for example possessive-like atomic groups `(?>…)`, inline options `(?i)`, `\A`/`\Z`, and character class subtraction `[a-z-[aeiou]]`. A pattern that passes the kernel's validation can therefore fail or behave differently in a TypeScript client or BFF. The message also embeds `exception.Message`, which the .NET resources localize, so the "same in every environment" remark of `ContractValidator` does not strictly hold for this code. Name the dialect in `StringType.Pattern` and validate with the matching option. If ECMAScript is chosen, `RegexOptions.ECMAScript` comes closest.

```csharp
/// <param name="Pattern">
/// A regular expression in ECMA-262 syntax, as in JSON Schema, that the value has to match;
/// <see langword="null"/> when any text is permitted.
/// </param>
public sealed record StringType(int? MaxLength = null, string? Pattern = null) : DataType;

// DataTypeRules.CheckString
_ = new Regex(pattern, RegexOptions.ECMAScript, PatternTimeout);
// ...
context.Add(
    ValidationContext.Member(path, "pattern"),
    ValidationCodes.InvalidPattern,
    $"The pattern '{pattern}' is not a valid regular expression.");
```

## Findings — src/SmartHal.Contracts/Serialization/IsoDurationConverter.cs

### [Minor][Code-Quality] src/SmartHal.Contracts/Serialization/IsoDurationConverter.cs:67
The duration pattern accepts a trailing line feed, and it matches non-ASCII digits that are then reported with a misleading message.

In .NET, `$` also matches before a final `\n`, and `XmlConvert.ToTimeSpan` trims whitespace. **FACT** (probe): `{"wakeInterval":"PT30S\n"}` is read as 30 seconds, while `" PT30S"` is rejected. `\d` matches every Unicode decimal digit, so `"PT٣S"` (Arabic-Indic three) passes the pattern, `XmlConvert` then rejects it, and the error reads "'PT٣S' is outside the range of a duration". The converter remarks and `ContractsJson` promise that only exact ISO 8601 durations are accepted. The package has not been released yet. After the first release, rejecting the trailing line feed narrows what the schema admits, and VERSIONING.md classifies that as breaking. Add `"PT30S\n"` and a non-ASCII digit to `Deserialize_DurationOutsideTheContract_ThrowsJsonException`.

```csharp
// \z: end of input only; [0-9]: ASCII digits only.
[GeneratedRegex(@"^P(?=[0-9]|T[0-9])([0-9]+D)?(T(?=[0-9])([0-9]+H)?([0-9]+M)?([0-9]+(\.[0-9]{1,7})?S)?)?\z", RegexOptions.CultureInvariant, 1000)]
private static partial Regex DurationPattern();
```

## Findings — src/SmartHal.Contracts/Serialization/StrictEnumConverterFactory.cs

### [Suggestion][Code-Quality] src/SmartHal.Contracts/Serialization/StrictEnumConverterFactory.cs:19
The factory takes over every enum, including enums of consumers that copy `ContractsJson.Options`. It fails hard on alias values and ignores `[JsonStringEnumMemberName]`.

`CanConvert` returns `true` for any enum. The `ContractsJson` remarks tell callers to copy the options when they need different settings, so a BFF that serializes its own DTOs with that copy also routes its own enums through this converter. **FACT** (probe):
- An enum with aliases (`A = 0, B = 0`) throws `TypeInitializationException`, because `ToFrozenDictionary` meets a duplicate key at line 31–32. The static initializer then stays broken for that type for the lifetime of the process, and the exception is not a `JsonException`.
- `[JsonStringEnumMemberName("foo-bar")]` is ignored, and `"foo"` is written instead.
- A `[Flags]` combination cannot be written.

None of the contract enums has these traits, so the contracts themselves work. Limit the factory to the package's own enums, and let every other enum fall back to what the consumer configures.

```csharp
/// <inheritdoc/>
public override bool CanConvert(Type typeToConvert) =>
    typeToConvert.IsEnum && typeToConvert.Assembly == typeof(StrictEnumConverterFactory).Assembly;
```

## Findings — tests/SmartHal.Core.UnitTests/Validation/ValueRulesTests.cs

### [Suggestion][Tests] tests/SmartHal.Core.UnitTests/Validation/ValueRulesTests.cs:163
`Validate_SleepyWakeInterval_IsReportedUnlessPositive` branches on the data row (`if (valid) … else …`). The previous review flagged the same pattern in the `TryParse` tests (s-5), and it has come back in a new test.

Each row asserts something different, so the theory hides two behaviours behind one name. Split it into a failure theory and a success fact, so that every test has one unconditional assertion (dotnet-tester convention).

```csharp
[Theory]
[InlineData(0)]
[InlineData(-1)]
public void Validate_SleepyWakeIntervalNotPositive_ReportsInvalidDuration(int seconds)
{
    // Arrange
    var deviceType = Dimmer() with { Sleepy = new SleepyConfig(TimeSpan.FromSeconds(seconds)) };

    // Act
    var errors = _sut.Validate(deviceType);

    // Assert
    errors.CodesAndPaths().Should().Equal((ValidationCodes.InvalidDuration, "sleepy.wakeInterval"));
}

[Fact]
public void Validate_SleepyWakeIntervalPositive_ReportsNoViolation()
{
    // Arrange
    var deviceType = Dimmer() with { Sleepy = new SleepyConfig(TimeSpan.FromMinutes(5)) };

    // Act
    var errors = _sut.Validate(deviceType);

    // Assert
    errors.Should().BeEmpty();
}
```

## Tool Output Appendix

### dotnet build
- Not run by the reviewer (build=no, provided). The caller reports `dotnet build SmartHal.slnx --no-incremental`: 0 errors, 0 warnings.

### dotnet test
- Not run by the reviewer (test=no, provided). The caller reports `dotnet test --solution SmartHal.slnx`: 911/911 passed. The caller also reports an app smoke check: the server started, reported ready, and shut down cleanly on SIGTERM with exit code 0.

### dotnet format
- Skipped (format=no, provided).

### Reviewer tooling notes
- `scripts/collect-diff.sh` again wrote the file name `"docs/Formales Schema (C#).md\t"` with a raw tab character into its JSON, which makes the output invalid JSON for a strict parser. It was parsed with `strict=False`, and the diff content was not affected. This is an issue in the review script, not in the repository.
- The verification probes were small console programs in the session scratchpad that referenced the built Debug assemblies. No repository file was modified apart from this report.
