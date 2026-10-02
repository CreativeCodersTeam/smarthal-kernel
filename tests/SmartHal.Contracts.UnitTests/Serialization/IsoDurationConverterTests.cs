using System.Text.Json;
using AwesomeAssertions;
using SmartHal.Contracts.Serialization;
using Xunit;

namespace SmartHal.Contracts.UnitTests.Serialization;

/// <summary>
/// Verifies that durations travel as ISO 8601 strings and that anything else is rejected with a
/// <see cref="JsonException"/>.
/// </summary>
public sealed class IsoDurationConverterTests
{
    public static TheoryData<string, TimeSpan> ValidDurations => new()
    {
        { "PT0S", TimeSpan.Zero },
        { "PT30S", TimeSpan.FromSeconds(30) },
        { "PT1H30M", new TimeSpan(1, 30, 0) },
        { "P90D", TimeSpan.FromDays(90) },
        { "P1DT2H3M4.5S", new TimeSpan(1, 2, 3, 4, 500) },
        { "PT0.0000001S", TimeSpan.FromTicks(1) }
    };

    [Theory]
    [MemberData(nameof(ValidDurations))]
    public void RoundTrip_ValidDuration_KeepsValueAndText(string text, TimeSpan expected)
    {
        // Arrange
        var json = $"\"{text}\"";

        // Act
        var value = JsonSerializer.Deserialize<TimeSpan>(json, ContractsJson.Options);
        var written = JsonSerializer.Serialize(value, ContractsJson.Options);

        // Assert
        value.Should().Be(expected);
        written.Should().Be(json);
    }

    [Theory]
    [InlineData("\"00:00:30\"")]
    [InlineData("\"30s\"")]
    [InlineData("\"\"")]
    [InlineData("30")]
    [InlineData("null")]
    [InlineData("\"P99999999999D\"")]
    [InlineData("\"-PT5S\"")]
    [InlineData("\"P1M\"")]
    [InlineData("\"P1Y\"")]
    [InlineData("\"P1Y2M3D\"")]
    [InlineData("\"PT\"")]
    [InlineData("\"P\"")]
    [InlineData("\"P1DT\"")]
    [InlineData("\"pt30s\"")]
    [InlineData("\" PT30S \"")]
    [InlineData("\"PT1.S\"")]
    public void Deserialize_InvalidDuration_ThrowsJsonException(string json)
    {
        // Arrange

        // Act
        var act = () => JsonSerializer.Deserialize<TimeSpan>(json, ContractsJson.Options);

        // Assert
        act.Should().Throw<JsonException>();
    }

    [Fact]
    public void Serialize_NegativeDuration_ThrowsJsonException()
    {
        // Arrange
        var value = TimeSpan.FromSeconds(-5);

        // Act
        var act = () => JsonSerializer.Serialize(value, ContractsJson.Options);

        // Assert
        act.Should().Throw<JsonException>();
    }
}
