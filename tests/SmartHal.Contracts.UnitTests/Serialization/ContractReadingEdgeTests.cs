using System.Text.Json;
using System.Text.Json.Serialization;
using AwesomeAssertions;
using SmartHal.Contracts.Addressing;
using SmartHal.Contracts.Bus;
using SmartHal.Contracts.DataTypes;
using SmartHal.Contracts.Integration;
using SmartHal.Contracts.Integration.Virtual;
using SmartHal.Contracts.Primitives;
using SmartHal.Contracts.Runtime;
using SmartHal.Contracts.Serialization;
using Xunit;

namespace SmartHal.Contracts.UnitTests.Serialization;

/// <summary>
/// Verifies the edges of the strict reading: discriminators on every kind of abstract contract type, derived types
/// read directly, enums as dictionary keys and in collections, and durations at the limits of a <see cref="TimeSpan"/>.
/// </summary>
public sealed class ContractReadingEdgeTests
{
    // A consumer's copy of the shared options with a lenient converter for its own enums; built once (CA1869).
    private static readonly JsonSerializerOptions CopyWithStringEnums = CreateCopyWithStringEnums();

    [Fact]
    public void Deserialize_DerivedTypeThroughItsOwnStaticType_ReadsItWithoutDiscriminator()
    {
        // Arrange
        const string number = """{"unit":"bar"}""";
        const string key = """{"device":"a","channel":"b","capability":"c","element":"d"}""";

        // Act
        var numberType = JsonSerializer.Deserialize<NumberType>(number, ContractsJson.Options);
        var keyAddress = JsonSerializer.Deserialize<KeyAddress>(key, ContractsJson.Options);

        // Assert
        numberType.Should().Be(new NumberType("bar"));
        keyAddress.Should().Be(new KeyAddress("a", "b", "c", "d"));
    }

    [Fact]
    public void Deserialize_BusMessageWithoutKind_ThrowsJsonException()
    {
        // Arrange
        const string json = """{"seq":1}""";

        // Act
        var act = () => JsonSerializer.Deserialize<BusMessage>(json, ContractsJson.Options);

        // Assert
        act.Should().Throw<JsonException>().WithMessage("*discriminator 'kind'*");
    }

    [Fact]
    public void Deserialize_SubstitutionWithoutPattern_ThrowsJsonException()
    {
        // Arrange
        const string json = """{"command":"toggle","memberType":"core.onoff@1","use":{"command":"toggle"}}""";

        // Act
        var act = () => JsonSerializer.Deserialize<Substitution>(json, ContractsJson.Options);

        // Assert
        act.Should().Throw<JsonException>().WithMessage("*discriminator 'pattern'*");
    }

    [Theory]
    [InlineData("""{"type":"object","fields":{"h":{"unit":"deg"}}}""")]
    [InlineData("""{"type":"array","items":{"maxLength":4}}""")]
    public void Deserialize_NestedDataTypeWithoutDiscriminator_ThrowsJsonException(string json)
    {
        // Arrange

        // Act
        var act = () => JsonSerializer.Deserialize<DataType>(json, ContractsJson.Options);

        // Assert
        act.Should().Throw<JsonException>().WithMessage("*discriminator 'type'*");
    }

    [Fact]
    public void Deserialize_TransformStepWithoutFnInsideMapping_ThrowsJsonException()
    {
        // Arrange
        const string json = """{"target":"a/b/c","address":"x","transform":[{"factor":2}]}""";

        // Act
        var act = () => JsonSerializer.Deserialize<Mapping>(json, ContractsJson.Options);

        // Assert
        act.Should().Throw<JsonException>().WithMessage("*discriminator 'fn'*");
    }

    [Theory]
    [InlineData("""{"type":null}""")]
    [InlineData("""{"type":5}""")]
    [InlineData("""{"type":{}}""")]
    [InlineData("""{"type":"number","type":"integer"}""")]
    [InlineData("""{"type":"number","unit":"bar","type":"string"}""")]
    public void Deserialize_DiscriminatorThatIsNotOneString_ThrowsJsonException(string json)
    {
        // Arrange

        // Act
        var act = () => JsonSerializer.Deserialize<DataType>(json, ContractsJson.Options);

        // Assert
        act.Should().Throw<JsonException>();
    }

    [Fact]
    public void Deserialize_AbstractTypeWithoutPolymorphism_ThrowsJsonException()
    {
        // Arrange
        const string json = """{"unit":"bar"}""";

        // Act
        var act = () => JsonSerializer.Deserialize<NumericType>(json, ContractsJson.Options);

        // Assert
        act.Should().Throw<JsonException>().WithMessage("*NumericType cannot be read directly*");
    }

    [Fact]
    public void Deserialize_DictionaryKeysDifferingOnlyInCase_AreDistinctKeys()
    {
        // Arrange
        const string json = """{"type":"object","fields":{"h":{"type":"number"},"H":{"type":"integer"}}}""";

        // Act
        var dataType = JsonSerializer.Deserialize<DataType>(json, ContractsJson.Options);

        // Assert
        dataType.Should().BeOfType<ObjectType>().Which.Fields.Keys.Should().BeEquivalentTo("h", "H");
    }

    [Fact]
    public void RoundTrip_EnumAsDictionaryKey_UsesTheSnakeCaseName()
    {
        // Arrange
        var map = new Dictionary<AlarmState, int> { [AlarmState.ActiveUnacked] = 2, [AlarmState.Cleared] = 5 };

        // Act
        var json = JsonSerializer.Serialize(map, ContractsJson.Options);
        var result = JsonSerializer.Deserialize<Dictionary<AlarmState, int>>(json, ContractsJson.Options);

        // Assert
        json.Should().Be("""{"active_unacked":2,"cleared":5}""");
        result.Should().BeEquivalentTo(map);
    }

    [Theory]
    [InlineData("""{"ActiveUnacked":1}""")]
    [InlineData("""{"0":1}""")]
    [InlineData("""{"active_unacked, cleared":1}""")]
    public void Deserialize_EnumDictionaryKeyNotInSnakeCase_ThrowsJsonException(string json)
    {
        // Arrange

        // Act
        var act = () => JsonSerializer.Deserialize<Dictionary<AlarmState, int>>(json, ContractsJson.Options);

        // Assert
        act.Should().Throw<JsonException>();
    }

    [Theory]
    [InlineData("""["info","Minor"]""")]
    [InlineData("""["info",2]""")]
    public void Deserialize_InvalidEnumInsideACollection_ThrowsJsonException(string json)
    {
        // Arrange

        // Act
        var act = () => JsonSerializer.Deserialize<List<Severity>>(json, ContractsJson.Options);

        // Assert
        act.Should().Throw<JsonException>();
    }

    [Fact]
    public void Deserialize_UnknownEnumName_NamesTheExpectedValues()
    {
        // Arrange
        const string json = "\"median\"";

        // Act
        var act = () => JsonSerializer.Deserialize<Aggregation>(json, ContractsJson.Options);

        // Assert
        act.Should().Throw<JsonException>().WithMessage("*'median'*any, all, avg, min, max, sum, last*");
    }

    [Fact]
    public void RoundTrip_TimeSpanMaxValue_KeepsTheValue()
    {
        // Arrange
        var value = TimeSpan.MaxValue;

        // Act
        var json = JsonSerializer.Serialize(value, ContractsJson.Options);
        var result = JsonSerializer.Deserialize<TimeSpan>(json, ContractsJson.Options);

        // Assert
        json.Should().Be("\"P10675199DT2H48M5.4775807S\"");
        result.Should().Be(value);
    }

    [Theory]
    [InlineData("\"P10675199DT2H48M5.4775808S\"")]
    [InlineData("\"PT9999999999999H\"")]
    [InlineData("\"PT99999999999999999999S\"")]
    [InlineData("\"PT0.00000001S\"")]
    [InlineData("\"PT1.5H\"")]
    [InlineData("\"PT1.5M\"")]
    [InlineData("\"P1.5D\"")]
    [InlineData("\"P1W\"")]
    [InlineData("\"PT30S1M\"")]
    [InlineData("\"PT1D\"")]
    [InlineData("\"PT0,5S\"")]
    [InlineData("\"PT30S\\n\"")]
    [InlineData("\"PT\u0663S\"")]
    [InlineData("\"P\uFF11D\"")]
    public void Deserialize_DurationOutsideTheContract_ThrowsJsonException(string json)
    {
        // Arrange

        // Act
        var act = () => JsonSerializer.Deserialize<TimeSpan>(json, ContractsJson.Options);

        // Assert
        act.Should().Throw<JsonException>();
    }

    [Theory]
    [InlineData("\"PT90M\"", "\"PT1H30M\"")]
    [InlineData("\"PT24H\"", "\"P1D\"")]
    [InlineData("\"PT1.1234567S\"", "\"PT1.1234567S\"")]
    public void RoundTrip_NonCanonicalDuration_IsWrittenInCanonicalForm(string json, string expected)
    {
        // Arrange

        // Act
        var value = JsonSerializer.Deserialize<TimeSpan>(json, ContractsJson.Options);
        var written = JsonSerializer.Serialize(value, ContractsJson.Options);

        // Assert
        written.Should().Be(expected);
    }

    [Fact]
    public void Serialize_OneTickBelowZero_ThrowsJsonException()
    {
        // Arrange
        var value = TimeSpan.FromTicks(-1);

        // Act
        var act = () => JsonSerializer.Serialize(value, ContractsJson.Options);

        // Assert
        act.Should().Throw<JsonException>();
    }

    [Fact]
    public void Deserialize_DurationWithNonAsciiDigit_ReportsTheFormNotTheRange()
    {
        // Arrange
        const string json = "\"PT\u0663S\"";

        // Act
        var act = () => JsonSerializer.Deserialize<TimeSpan>(json, ContractsJson.Options);

        // Assert
        act.Should().Throw<JsonException>().WithMessage("*is not a non-negative ISO 8601 duration*");
    }

    [Fact]
    public void RoundTrip_EnumOfAnotherAssembly_KeepsTheDefaultHandling()
    {
        // Arrange
        // DayOfWeek lives in System.Private.CoreLib: the strict contract rules do not apply to it.
        var value = new ForeignEnumHolder(DayOfWeek.Monday);

        // Act
        var json = JsonSerializer.Serialize(value, ContractsJson.Options);
        var result = JsonSerializer.Deserialize<ForeignEnumHolder>(json, ContractsJson.Options);

        // Assert
        json.Should().Be("""{"day":1}""");
        result.Should().Be(value);
    }

    [Fact]
    public void RoundTrip_EnumWithAliasOfAnotherAssembly_DoesNotFail()
    {
        // Arrange
        var value = new AliasHolder(AliasedLevel.Medium);

        // Act
        var json = JsonSerializer.Serialize(value, ContractsJson.Options);
        var result = JsonSerializer.Deserialize<AliasHolder>(json, ContractsJson.Options);

        // Assert
        result!.Level.Should().Be(AliasedLevel.Medium);
    }

    [Theory]
    [InlineData("\"PT0.\u0663S\"")]
    [InlineData("\"PT\u0663H\"")]
    [InlineData("\"PT\u0663M\"")]
    [InlineData("\"PT30S\\n\"")]
    public void Deserialize_DurationWithNonAsciiDigitOrTrailingLineFeed_IsRejectedByTheForm(string json)
    {
        // Arrange

        // Act
        var act = () => JsonSerializer.Deserialize<TimeSpan>(json, ContractsJson.Options);

        // Assert
        act.Should().Throw<JsonException>().WithMessage("*is not a non-negative ISO 8601 duration*");
    }

    [Fact]
    public void RoundTrip_ForeignEnumInACopyWithStringEnumConverter_UsesTheConsumersConverter()
    {
        // Arrange
        var value = new ColorHolder(ConsumerColor.DarkRed);

        // Act
        var json = JsonSerializer.Serialize(value, CopyWithStringEnums);
        var result = JsonSerializer.Deserialize<ColorHolder>(json, CopyWithStringEnums);

        // Assert
        json.Should().Be("""{"color":"dark-red"}""");
        result.Should().Be(value);
    }

    [Theory]
    [InlineData("\"Critical\"")]
    [InlineData("4")]
    public void Deserialize_ContractEnumInACopyWithStringEnumConverter_StaysStrict(string severity)
    {
        // Arrange
        var json = $$"""{"severity":{{severity}}}""";

        // Act
        var act = () => JsonSerializer.Deserialize<SeverityHolder>(json, CopyWithStringEnums);

        // Assert
        act.Should().Throw<JsonException>();
    }

    [Fact]
    public void RoundTrip_UndeclaredValueOfAForeignEnum_IsWrittenAsNumber()
    {
        // Arrange
        var value = new ForeignEnumHolder((DayOfWeek)99);

        // Act
        var json = JsonSerializer.Serialize(value, ContractsJson.Options);
        var result = JsonSerializer.Deserialize<ForeignEnumHolder>(json, ContractsJson.Options);

        // Assert
        json.Should().Be("""{"day":99}""");
        result.Should().Be(value);
    }

    [Fact]
    public void Serialize_ForeignEnumAsDictionaryKey_KeepsTheDefaultKeyHandling()
    {
        // Arrange
        var map = new Dictionary<DayOfWeek, int> { [DayOfWeek.Monday] = 1 };

        // Act
        var json = JsonSerializer.Serialize(map, ContractsJson.Options);

        // Assert
        json.Should().Be("""{"Monday":1}""");
    }

    private static JsonSerializerOptions CreateCopyWithStringEnums()
    {
        var options = new JsonSerializerOptions(ContractsJson.Options);
        options.Converters.Add(new JsonStringEnumConverter());

        return options;
    }

    private enum ConsumerColor
    {
        Red = 0,

        [JsonStringEnumMemberName("dark-red")]
        DarkRed = 1
    }

    private sealed record ColorHolder(ConsumerColor Color);

    private sealed record SeverityHolder(Severity Severity);

    private enum AliasedLevel
    {
        Low = 0,
        Medium = 1,
        Normal = Medium
    }

    private sealed record ForeignEnumHolder(DayOfWeek Day);

    private sealed record AliasHolder(AliasedLevel Level);
}
