# .NET Code Review — feature/device-catalog (uncommitted)

**Date:** 2026-09-30
**Mode:** uncommitted (origin: provided)
**Detected SDK:** 10.0.0 (installed SDK used for verification probes: 10.0.401)
**Target Framework(s):** net10.0
**Version origin:** repo:Directory.Build.props
**Checklist:** review-checklist-net10.md, plus the general checklists (security, performance, architecture, code-quality)
**Tools run:** build=N · format=N · test=N (origin: provided; the caller reports `dotnet build SmartHal.slnx --no-incremental` with 0 errors / 0 warnings and `dotnet test --solution SmartHal.slnx` with 815/815 passed)
**Report language:** English (origin: provided)
**Exclusions:** .gitignore, *.min.js, wwwroot/lib/**
**Review strategy:** chunked (auto-selected, non-interactive; the diff exceeds the gate of 2000 LOC / 50 files)
**Diff size:** 177 files, 11284 changed LOC

## Executive Summary

| Severity | Count |
|---|---|
| Critical | 0 |
| Major | 2 |
| Minor | 3 |
| Suggestion | 5 |
| Nitpick | 1 |

**Top risks:**
1. The shared enum converter accepts comma-separated values and C# member names. For example `"sent, acked"` is read as `CommandStatus.Completed`, so malformed input turns into a valid but wrong state without an error (ContractsJson.cs:49).
2. Rule R1 (struct depth ≤ 2) can be bypassed with a `RefType`, and cyclic data type references pass validation. A later consumer that expands refs (for example history decomposition) has no bound on recursion (DataTypeRules.cs:148).
3. Strictness gaps in `ContractsJson.Options`: duplicate JSON properties and dictionary keys are resolved last-wins without an error, and a missing discriminator throws `NotSupportedException` instead of the documented `JsonException`. The JSON form is a published contract under VERSIONING.md, so tightening it after the first release counts as a breaking change. It is cheaper to do now.

**Overall:** The change is well structured and well documented. The contracts follow the formal schema, including the approved deviations. The validator collects errors and does not throw on invalid content, and the unit tests cover every validation code. The two Major findings are contained: one is a fix in the options, the other an addition to the validator. Both should be fixed before the first package with these types is released, because the JSON reading behaviour becomes part of the public contract with that release.

**Scope notes:**
- `docs/IoT-Domänenmodell.md` and `docs/Formales Schema (C#).md` served as the specification and were not reviewed as code.
- The blank line at `src/SmartHal.Server/Composition/ServerHost.cs:61` is the user's own earlier change and is not reported.
- Deliberate, user-approved decisions are not reported as defects: web-default NumberHandling, one catalog entry per name+major, records without constructor guards, no host-wiring integration test, strict enums for unknown names, and schema deviations B1/B2/B5/B7/B10 adopted while B3/B4/B6/B8/B9 were not.
- VERSIONING.md classification: every public type in `SmartHal.Contracts` is new, which makes this a *Not breaking (Minor)* change. The `Description` and README of the package were updated (FR-71).
- **FACT** labels mark behaviour that was verified by running a probe program against the built `SmartHal.Contracts`, `SmartHal.Core` and `SmartHal.Core.Abstractions` assemblies on the installed .NET 10.0.401 SDK. The probe lived in the session scratchpad; nothing in the repository was changed.

## Findings — src/SmartHal.Contracts/Serialization/ContractsJson.cs

### [Major][Security] src/SmartHal.Contracts/Serialization/ContractsJson.cs:49
The enum converter accepts comma-separated flag combinations and C# member names, so malformed input becomes a valid but wrong enum value.

`JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower, allowIntegerValues: false)` falls back to `Enum.TryParse`-style parsing when it reads. **FACT** (probe):
- `{"status":"sent, acked"}` is read as `CommandStatus.Completed` (1 | 2 = 3).
- `{"status":"pending,completed"}` is read as `Completed`.
- `{"state":"active_acked, cleared_unacked"}` is read as `AlarmState.Cleared`.
- `{"completion":"ack, confirmed"}` is read as `Completion.Confirmed`.
- `{"level":"good, bad"}` is read as `QualityLevel.Bad`.
- `"Good"` and `"ActiveUnacked"` (C# member names) are accepted. `"ACTIVE_UNACKED"` and `"foo"` are rejected.

None of the contract enums is a `[Flags]` enum, so a combined value has no meaning. The remarks at lines 15–16 promise that "unknown names are rejected with a JsonException", and the code does not keep that promise. For state-machine enums (`CommandStatus`, `AlarmState`) this lets a client or adapter slip a terminal state past the reader (OWASP A04 Insecure Design / A08 Data Integrity). The package is not yet released. Tightening the reader after release narrows what the schema admits, which VERSIONING.md classifies as breaking, so fix it before the first release. Add tests for comma lists and for PascalCase names next to `Deserialize_UnknownEnumName_ThrowsJsonException`.

```csharp
// Serialization/StrictEnumConverterFactory.cs (internal): exact snake_case names only
internal sealed class StrictEnumConverterFactory : JsonConverterFactory
{
    public override bool CanConvert(Type typeToConvert) => typeToConvert.IsEnum;

    public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options) =>
        (JsonConverter)Activator.CreateInstance(typeof(StrictEnumConverter<>).MakeGenericType(typeToConvert))!;
}

internal sealed class StrictEnumConverter<TEnum> : JsonConverter<TEnum> where TEnum : struct, Enum
{
    private static readonly Dictionary<string, TEnum> ByName = Enum.GetValues<TEnum>()
        .ToDictionary(value => JsonNamingPolicy.SnakeCaseLower.ConvertName(value.ToString()), StringComparer.Ordinal);

    private static readonly Dictionary<TEnum, string> ByValue = ByName.ToDictionary(pair => pair.Value, pair => pair.Key);

    public override TEnum Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.String)
        {
            throw new JsonException($"A value of {typeof(TEnum).Name} must be a JSON string, but the token is {reader.TokenType}.");
        }

        var text = reader.GetString()!;

        return ByName.TryGetValue(text, out var value)
            ? value
            : throw new JsonException($"'{text}' is not a value of {typeof(TEnum).Name}.");
    }

    public override void Write(Utf8JsonWriter writer, TEnum value, JsonSerializerOptions options) =>
        writer.WriteStringValue(ByValue.TryGetValue(value, out var name)
            ? name
            : throw new JsonException($"{value} is not a defined value of {typeof(TEnum).Name}."));
}

// ContractsJson.CreateOptions()
Converters =
{
    new StrictEnumConverterFactory(),
    new IsoDurationConverter()
}
```

### [Minor][Security] src/SmartHal.Contracts/Serialization/ContractsJson.cs:41
Duplicate JSON properties and duplicate dictionary keys are accepted silently, and the last one wins.

**FACT** (probe): `ContractsJson.Options.AllowDuplicateProperties` is `true`, the .NET 10 default. `{"fields":{"a":{"type":"boolean"},"a":{"type":"string"}}}` is read as an `ObjectType` whose field `a` is a `StringType`, and `{"level":"good","level":"bad"}` is read as `Bad`. The rest of the options opt into strict reading (`RespectNullableAnnotations`, `RespectRequiredConstructorParameters`), and the validator reports `duplicate_key` / `duplicate_enum_value` for lists. A duplicate key in a map such as `properties`, `commands` or `fields` is the same authoring mistake, but the reader loses it before the validator can see it. Rejecting duplicates after release narrows what the schema admits (breaking under VERSIONING.md), so decide now.

```csharp
var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
{
    AllowDuplicateProperties = false, // .NET 10: duplicate properties / dictionary keys -> JsonException
    AllowOutOfOrderMetadataProperties = true,
    RespectNullableAnnotations = true,
    RespectRequiredConstructorParameters = true,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    // ...
};
```

### [Minor][Code-Quality] src/SmartHal.Contracts/Serialization/ContractsJson.cs:21
A missing type discriminator fails with `NotSupportedException`, but the documented failure mode of the options is `JsonException` only.

**FACT**: the tests `Deserialize_MissingDiscriminator_ThrowsNotSupportedException` (ContractStrictnessTests, ElementRefSerializationTests, SchemaContractTests) confirm that JSON without `type`/`kind`/`by`/`fn`/`pattern` for an abstract base throws `NotSupportedException`. A BFF or server endpoint that maps `JsonException` to 400 Bad Request will return a 500 for this client error. The remarks of `ContractsJson` list the cases that throw `JsonException` and do not mention this one, and neither does the README. Document it. Better, give consumers a single entry point that normalizes the exception.

```csharp
/// <item><description>
/// an object of a polymorphic contract (data type, alarm source, element reference, transform, substitution,
/// bus message, subscription item) without its discriminator is rejected with a <see cref="NotSupportedException"/>
/// by System.Text.Json; <see cref="Deserialize{T}(string)"/> reports it as a <see cref="JsonException"/>.
/// </description></item>

public static T? Deserialize<T>(string json)
{
    try
    {
        return JsonSerializer.Deserialize<T>(json, Options);
    }
    catch (NotSupportedException exception)
    {
        throw new JsonException(exception.Message, exception);
    }
}
```

## Findings — src/SmartHal.Core/Validation/DataTypeRules.cs

### [Major][Code-Quality] src/SmartHal.Core/Validation/DataTypeRules.cs:148
Rule R1 (struct depth) does not follow `RefType` references, and cyclic data type references are not detected.

`CheckReference` only checks that the referenced name exists (`CatalogIndex.HasDataType` keeps names only, CatalogIndex.cs:26/44). The depth of the referenced `DataTypeDef` is never added to the depth of the struct that refers to it. **FACT** (probe): a catalog with a capability property `object → object → ref(x.deep@1)`, where `x.deep@1` is `object → object`, describes a struct four levels deep, and `Validate(TypeCatalog)` returns no errors. A `DataTypeDef` `x.cyc@1 = object { self: ref(x.cyc@1) }` also returns no errors. The domain model states "Structs sind höchstens zwei Ebenen tief" so that history decomposition and bindings stay manageable. Once vendor types arrive through adapters, any consumer that expands refs (history decomposition into `color.h`, `color.s`, …) can recurse without bound on a validated catalog. Keep the definitions in the index and resolve refs with a visited set, counting the struct levels of the target.

```csharp
// CatalogIndex: keep the definitions, not only the names
private readonly Dictionary<(string Name, int Major), DataTypeDef> _dataTypes;
public DataTypeDef? FindDataType(TypeRef reference) => _dataTypes.GetValueOrDefault((reference.Name, reference.Major));

// DataTypeRules
private static void CheckReference(
    RefType refType, string path, ValidationContext context, CatalogIndex? index, int depth, HashSet<TypeRef> resolving)
{
    if (index is null)
    {
        return;
    }

    var definition = index.FindDataType(refType.Ref);

    if (definition is null)
    {
        context.Add(ValidationContext.Member(path, "ref"), ValidationCodes.UnresolvedType,
            $"The data type '{refType.Ref}' is not in the type catalog.");
        return;
    }

    if (!resolving.Add(refType.Ref))
    {
        context.Add(ValidationContext.Member(path, "ref"), ValidationCodes.CyclicReference, // new, additive code
            $"The data type '{refType.Ref}' refers to itself.");
        return;
    }

    // Only depth is judged through the reference; the definition's own rules are reported where it is defined.
    CheckDepthOnly(definition.DataType, path, context, index, depth, resolving);
    resolving.Remove(refType.Ref);
}
```

### [Minor][Code-Quality] src/SmartHal.Core/Validation/DataTypeRules.cs:123
Validation messages format numbers with the current culture.

**FACT** (probe): with `CurrentCulture = de-DE`, the message reads "The minimum 1,5 is greater than the maximum 0,5." The same interpolation is used at lines 131 and 143. The rest of the code formats deliberately with `InvariantCulture` (`ValidationContext.Index`, `TypeRef.ToString`, `TypeVersion.ToString`). The server can run under any OS locale, so the same catalog produces different messages on different hosts, and the English sentence gets a German decimal separator.

```csharp
context.Add(
    ValidationContext.Member(path, "minimum"),
    ValidationCodes.InvalidRange,
    string.Create(CultureInfo.InvariantCulture, $"The minimum {minimum} is greater than the maximum {maximum}."));
```

### [Suggestion][Code-Quality] src/SmartHal.Core/Validation/DataTypeRules.cs:45
Several self-contradicting constraints are not covered by R1–R15.

**FACT** (probe): the validator returns no errors for `StringType(MaxLength: -1, Pattern: "(")` (negative length, invalid regex), `ArrayType(…, MaxItems: -3)`, a `CommandDef` with `Timeout = -5 s`, or a `HistoryPolicy` with `RawRetention = -1 d`. `SleepyConfig.WakeInterval`, `Rollup.Interval/Retention` and `Deadband` values are not checked either. These are data-type and schema constraints of the same kind that R2 already covers for numeric ranges and steps. New rules with new codes are additive and non-breaking.

```csharp
case StringType stringType:
    if (stringType.MaxLength is < 0)
    {
        context.Add(ValidationContext.Member(path, "maxLength"), ValidationCodes.InvalidRange, "The maximum length is negative.");
    }

    if (stringType.Pattern is { } pattern && !IsValidRegex(pattern))
    {
        context.Add(ValidationContext.Member(path, "pattern"), ValidationCodes.InvalidPattern, "The pattern is not a valid regular expression.");
    }

    break;
case ArrayType arrayType:
    if (arrayType.MaxItems is < 0) { /* invalid_range */ }
    Check(arrayType.Items, ValidationContext.Member(path, "items"), context, index, depth);
    break;
```

## Findings — src/SmartHal.Contracts/DataTypes/EnumType.cs

### [Suggestion][Code-Quality] src/SmartHal.Contracts/DataTypes/EnumType.cs:10
Records with collection members compare by reference, not by value, and this is not documented.

**FACT** (probe): `new EnumType(["a"]) == new EnumType(["a"])` is `false`, and `CoreCapabilityCatalog.All[0] == CoreCapabilityCatalog.All[0]` is `false` (because each access builds fresh graphs). The same applies to every contract that has an `IReadOnlyList<>`/`IReadOnlyDictionary<>`/`JsonNode` member (`CapabilityType`, `ObjectType`, `Device`, `CommandInvocation`, …). Consumers of an "immutable positional record" package reasonably expect value equality, for example to detect catalog changes or to deduplicate. Document the behaviour in the README (and in `<remarks>` of the affected base types), or offer a structural comparison. Otherwise the reference semantics will surface as subtle bugs in the BFF.

```markdown
<!-- src/SmartHal.Contracts/README.md -->
## Equality

The contracts are records, but members that are collections or `JsonNode` values compare by reference.
Two contracts read from the same JSON are therefore not `==`. Compare their JSON form
(`JsonSerializer.SerializeToNode(x, ContractsJson.Options)` with `JsonNode.DeepEquals`) when you need structural equality.
```

## Findings — src/SmartHal.Contracts/Primitives/TypeRef.cs

### [Suggestion][Architecture] src/SmartHal.Contracts/Primitives/TypeRef.cs:17
The namespaces `Primitives` and `Serialization` depend on each other.

`Primitives.TypeRef`/`TypeVersion` name `Serialization.TypeRefConverter`/`TypeVersionConverter` in `[JsonConverter]`, and those converters import `Primitives`. Every other namespace of the package forms a clean DAG (Addressing → –, DataTypes → Primitives, Schema → DataTypes/Integration/Primitives, …). This cycle is the only exception. It is harmless inside one assembly, but it blocks a namespace architecture rule of the form "Primitives depends on nothing". One option is to move the two type-specific converters next to their types, leaving `Serialization` for the options and the general `IsoDurationConverter`. The other is to accept the cycle and exempt it explicitly in `NamespaceRules`.

```csharp
// src/SmartHal.Contracts/Primitives/TypeRefConverter.cs
namespace SmartHal.Contracts.Primitives;

public sealed class TypeRefConverter : JsonConverter<TypeRef> { /* unchanged */ }
```

## Findings — src/SmartHal.Contracts/Serialization/IsoDurationConverter.cs

### [Suggestion][Code-Quality] src/SmartHal.Contracts/Serialization/IsoDurationConverter.cs:26
`XmlConvert.ToTimeSpan` accepts calendar designators and negative durations, and approximates the calendar designators silently.

**FACT** (probe): `"P1M"` is read as 30 days, and `"P1Y"` is read as 365 days and written back as `"P365D"`. `"-PT1S"` is accepted for `SleepyConfig.WakeInterval`. Every duration in the contracts (timeouts, retention, wake interval, poll interval, deadband) is non-negative, and a month or year has no fixed length. Reject both when reading, or at least document the approximation in the converter's `<remarks>`.

```csharp
var text = reader.GetString()!;

if (text.StartsWith('-') || HasCalendarDesignator(text))
{
    throw new JsonException($"'{text}' is not a non-negative ISO 8601 duration without years or months, such as 'PT30S' or 'P1D'.");
}

// Y and M before the time designator T are calendar units; M after T means minutes.
static bool HasCalendarDesignator(string text)
{
    var datePart = text.Split('T')[0];
    return datePart.Contains('Y') || datePart.Contains('M');
}
```

## Findings — tests/SmartHal.Contracts.UnitTests/Primitives/TypeRefTests.cs

### [Suggestion][Tests] tests/SmartHal.Contracts.UnitTests/Primitives/TypeRefTests.cs:82
The test body has conditional logic (`if (!expected)`). The same pattern appears in `tests/SmartHal.Contracts.UnitTests/Primitives/TypeVersionTests.cs:79`.

`TryParse_Text_ReportsWhetherTheTextIsValid` asserts different things depending on the data row, so the assertion on the out value runs only for some rows. Split the test into a success theory and a failure theory so that each has an unconditional assertion (the dotnet-tester convention).

```csharp
[Theory]
[InlineData("core.onoff")]
[InlineData(null)]
public void TryParse_InvalidText_ReturnsFalseAndDefault(string? text)
{
    // Act
    var success = TypeRef.TryParse(text, out var result);

    // Assert
    success.Should().BeFalse();
    result.Should().Be(default(TypeRef));
}

[Fact]
public void TryParse_ValidText_ReturnsTrue()
{
    // Act
    var success = TypeRef.TryParse("core.onoff@1", out var result);

    // Assert
    success.Should().BeTrue();
    result.Should().Be(new TypeRef("core.onoff", 1));
}
```

## Findings — src/SmartHal.Core/Catalog/CoreDataTypes.cs

### [Nitpick][Code-Quality] src/SmartHal.Core/Catalog/CoreDataTypes.cs:35
`HsvRef` repeats the literal name and major version of `Hsv` instead of deriving them.

If the name or the major version of `Hsv` changes, `HsvRef` can drift apart unnoticed. At the moment only a catalog test would catch it.

```csharp
private const string HsvName = "core.types.hsv";

public static DataTypeDef Hsv => new(HsvName, CoreVersion, /* ... */);

public static TypeRef HsvRef => new(HsvName, CoreVersion.Major);
```

## Tool Output Appendix

### dotnet build
- Not run by the reviewer (build=no, provided). The caller reports `dotnet build SmartHal.slnx --no-incremental`: 0 errors, 0 warnings.

### dotnet test
- Not run by the reviewer (test=no, provided). The caller reports `dotnet test --solution SmartHal.slnx`: 815/815 passed.

### dotnet format
- Skipped (format=no, provided).

### Reviewer tooling notes
- `scripts/collect-diff.sh` wrote a file name with a raw tab character (`"docs/Formales Schema (C#).md\t"`) into its JSON output, which makes the output invalid JSON for a strict parser. The output was parsed with `strict=False`, and the diff content was unaffected. This is an issue in the review script, not in the repository.
- The verification probes (a small console program referencing the built Debug assemblies) ran in the session scratchpad only. No repository file was modified apart from this report.
