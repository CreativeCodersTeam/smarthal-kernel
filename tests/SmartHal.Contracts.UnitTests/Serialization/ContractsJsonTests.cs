using System.Text.Json;
using AwesomeAssertions;
using SmartHal.Contracts.DataTypes;
using SmartHal.Contracts.Primitives;
using SmartHal.Contracts.Runtime;
using SmartHal.Contracts.Serialization;
using Xunit;

namespace SmartHal.Contracts.UnitTests.Serialization;

/// <summary>
/// Verifies the shared serializer options: read-only, enums as strict snake_case strings, durations in ISO 8601,
/// no <see langword="null"/> properties and discriminators in any position.
/// </summary>
public sealed class ContractsJsonTests
{
    [Fact]
    public void Options_Always_AreReadOnly()
    {
        // Arrange
        var options = ContractsJson.Options;

        // Act
        var act = () => options.WriteIndented = true;

        // Assert
        options.IsReadOnly.Should().BeTrue();
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Serialize_Enum_WritesSnakeCaseString()
    {
        // Arrange
        var value = new EnumHolder(Aggregation.Avg, Severity.Critical);

        // Act
        var json = JsonSerializer.Serialize(value, ContractsJson.Options);

        // Assert
        json.Should().Be("""{"aggregation":"avg","severity":"critical"}""");
    }

    [Fact]
    public void Deserialize_EnumAsInteger_ThrowsJsonException()
    {
        // Arrange
        const string json = """{"aggregation":2,"severity":"info"}""";

        // Act
        var act = () => JsonSerializer.Deserialize<EnumHolder>(json, ContractsJson.Options);

        // Assert
        act.Should().Throw<JsonException>();
    }

    [Fact]
    public void Deserialize_UnknownEnumName_ThrowsJsonException()
    {
        // Arrange
        const string json = """{"aggregation":"median","severity":"info"}""";

        // Act
        var act = () => JsonSerializer.Deserialize<EnumHolder>(json, ContractsJson.Options);

        // Assert
        act.Should().Throw<JsonException>();
    }

    [Fact]
    public void Serialize_NullProperty_OmitsIt()
    {
        // Arrange
        DataType value = new StringType(MaxLength: 16);

        // Act
        var json = JsonSerializer.Serialize(value, ContractsJson.Options);

        // Assert
        json.Should().Be("""{"type":"string","maxLength":16}""");
    }

    [Fact]
    public void Serialize_TimeSpan_WritesIsoDuration()
    {
        // Arrange
        var value = new DurationHolder(TimeSpan.FromSeconds(30), null);

        // Act
        var json = JsonSerializer.Serialize(value, ContractsJson.Options);

        // Assert
        json.Should().Be("""{"timeout":"PT30S"}""");
    }

    [Fact]
    public void Deserialize_NullableTimeSpan_ReadsIsoDurationAndNull()
    {
        // Arrange
        const string json = """{"timeout":"PT1M","pollInterval":"PT0.5S"}""";

        // Act
        var value = JsonSerializer.Deserialize<DurationHolder>(json, ContractsJson.Options);

        // Assert
        value.Should().Be(new DurationHolder(TimeSpan.FromMinutes(1), TimeSpan.FromMilliseconds(500)));
    }

    [Fact]
    public void Deserialize_DiscriminatorAfterOtherProperties_ReadsDerivedType()
    {
        // Arrange
        const string json = """{"unit":"bar","minimum":0,"type":"number"}""";

        // Act
        var value = JsonSerializer.Deserialize<DataType>(json, ContractsJson.Options);

        // Assert
        value.Should().Be(new NumberType(Unit: "bar", Minimum: 0));
    }

    [Fact]
    public void Deserialize_PropertyNamesInPascalCase_AreMatchedCaseInsensitively()
    {
        // Arrange
        const string json = """{"Aggregation":"any","Severity":"warning"}""";

        // Act
        var value = JsonSerializer.Deserialize<EnumHolder>(json, ContractsJson.Options);

        // Assert
        value.Should().Be(new EnumHolder(Aggregation.Any, Severity.Warning));
    }

    [Theory]
    [InlineData("\"sent, acked\"")]
    [InlineData("\"pending,sent\"")]
    [InlineData("\"Sent\"")]
    [InlineData("\"SENT\"")]
    [InlineData("\"ActiveUnacked\"")]
    [InlineData("\"\"")]
    [InlineData("\" sent\"")]
    [InlineData("null")]
    [InlineData("true")]
    public void Deserialize_EnumNotGivenAsItsExactSnakeCaseName_ThrowsJsonException(string value)
    {
        // Arrange
        var json = $$"""{"status":{{value}}}""";

        // Act
        var act = () => JsonSerializer.Deserialize<StatusHolder>(json, ContractsJson.Options);

        // Assert
        act.Should().Throw<JsonException>();
    }

    [Theory]
    [InlineData("\"active_unacked\"", AlarmState.ActiveUnacked)]
    [InlineData("\"cleared_unacked\"", AlarmState.ClearedUnacked)]
    public void Deserialize_EnumAsExactSnakeCaseName_ReadsTheValue(string value, AlarmState expected)
    {
        // Arrange
        var json = $$"""{"state":{{value}}}""";

        // Act
        var holder = JsonSerializer.Deserialize<AlarmStateHolder>(json, ContractsJson.Options);

        // Assert
        holder!.State.Should().Be(expected);
    }

    [Fact]
    public void Serialize_UndeclaredEnumValue_ThrowsJsonException()
    {
        // Arrange
        var value = new EnumHolder(Aggregation.Avg, (Severity)99);

        // Act
        var act = () => JsonSerializer.Serialize(value, ContractsJson.Options);

        // Assert
        act.Should().Throw<JsonException>();
    }

    [Fact]
    public void RoundTrip_NullableEnum_KeepsValueAndNull()
    {
        // Arrange
        var withValue = new NullableHolder(Severity.Minor);
        var withoutValue = new NullableHolder(null);

        // Act
        var first = JsonSerializer.Serialize(withValue, ContractsJson.Options);
        var second = JsonSerializer.Serialize(withoutValue, ContractsJson.Options);

        // Assert
        first.Should().Be("""{"severity":"minor"}""");
        second.Should().Be("{}");
        JsonSerializer.Deserialize<NullableHolder>(first, ContractsJson.Options).Should().Be(withValue);
        JsonSerializer.Deserialize<NullableHolder>(second, ContractsJson.Options).Should().Be(withoutValue);
    }

    [Theory]
    [InlineData("""{"aggregation":"avg","severity":"info","severity":"critical"}""")]
    [InlineData("""{"aggregation":"avg","Aggregation":"max","severity":"info"}""")]
    public void Deserialize_DuplicateProperty_ThrowsJsonException(string json)
    {
        // Arrange

        // Act
        var act = () => JsonSerializer.Deserialize<EnumHolder>(json, ContractsJson.Options);

        // Assert
        act.Should().Throw<JsonException>();
    }

    [Fact]
    public void Deserialize_DuplicateDictionaryKey_ThrowsJsonException()
    {
        // Arrange
        const string json = """{"type":"object","fields":{"h":{"type":"number"},"h":{"type":"integer"}}}""";

        // Act
        var act = () => JsonSerializer.Deserialize<DataType>(json, ContractsJson.Options);

        // Assert
        act.Should().Throw<JsonException>();
    }

    private sealed record EnumHolder(Aggregation Aggregation, Severity Severity);

    private sealed record DurationHolder(TimeSpan Timeout, TimeSpan? PollInterval);

    private sealed record StatusHolder(CommandStatus Status);

    private sealed record AlarmStateHolder(AlarmState State);

    private sealed record NullableHolder(Severity? Severity = null);
}
