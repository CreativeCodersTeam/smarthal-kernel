using AwesomeAssertions;
using SmartHal.Contracts.Primitives;
using Xunit;

namespace SmartHal.Contracts.UnitTests.Primitives;

/// <summary>
/// Verifies the text form <c>&lt;major&gt;.&lt;minor&gt;</c> of a type version in both directions.
/// </summary>
public sealed class TypeVersionTests
{
    [Theory]
    [InlineData("1.0", 1, 0)]
    [InlineData("2.1", 2, 1)]
    [InlineData("10.25", 10, 25)]
    [InlineData("2147483647.2147483647", int.MaxValue, int.MaxValue)]
    public void Parse_ValidText_ReturnsMajorAndMinor(string text, int expectedMajor, int expectedMinor)
    {
        // Arrange

        // Act
        var result = TypeVersion.Parse(text);

        // Assert
        result.Should().Be(new TypeVersion(expectedMajor, expectedMinor));
    }

    [Theory]
    [InlineData("1")]
    [InlineData("1.")]
    [InlineData(".1")]
    [InlineData("1.2.3")]
    [InlineData("-1.0")]
    [InlineData("1.-2")]
    [InlineData("a.b")]
    [InlineData(" 1.0")]
    [InlineData("1.0 ")]
    [InlineData("+1.0")]
    [InlineData("1..0")]
    [InlineData("2147483648.0")]
    [InlineData("1.99999999999")]
    [InlineData("")]
    public void Parse_InvalidText_ThrowsFormatExceptionNamingTheInput(string text)
    {
        // Arrange

        // Act
        var act = () => TypeVersion.Parse(text);

        // Assert
        act.Should().Throw<FormatException>().WithMessage($"*'{text}'*");
    }

    [Fact]
    public void Parse_Null_ThrowsArgumentNullException()
    {
        // Arrange

        // Act
        var act = () => TypeVersion.Parse(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void TryParse_ValidText_ReturnsTrueAndTheValue()
    {
        // Arrange

        // Act
        var success = TypeVersion.TryParse("1.2", out var result);

        // Assert
        success.Should().BeTrue();
        result.Should().Be(new TypeVersion(1, 2));
    }

    [Theory]
    [InlineData("1")]
    [InlineData(null)]
    public void TryParse_InvalidText_ReturnsFalseAndTheDefault(string? text)
    {
        // Arrange

        // Act
        var success = TypeVersion.TryParse(text, out var result);

        // Assert
        success.Should().BeFalse();
        result.Should().Be(default(TypeVersion));
    }

    [Fact]
    public void ToString_Version_ReturnsMajorDotMinor()
    {
        // Arrange
        var version = new TypeVersion(2, 1);

        // Act
        var text = version.ToString();

        // Assert
        text.Should().Be("2.1");
    }

    [Fact]
    public void TryParse_ThroughIParsable_IgnoresTheFormatProvider()
    {
        // Arrange
        var provider = new System.Globalization.CultureInfo("de-DE");

        // Act
        var success = TryParseGeneric<TypeVersion>("3.4", provider, out var result);

        // Assert
        success.Should().BeTrue();
        result.Should().Be(new TypeVersion(3, 4));
    }

    private static bool TryParseGeneric<T>(string text, IFormatProvider provider, out T? result)
        where T : IParsable<T> => T.TryParse(text, provider, out result);
}
