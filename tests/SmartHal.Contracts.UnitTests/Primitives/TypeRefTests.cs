using AwesomeAssertions;
using SmartHal.Contracts.Primitives;
using Xunit;

namespace SmartHal.Contracts.UnitTests.Primitives;

/// <summary>
/// Verifies the text form <c>&lt;name&gt;@&lt;major&gt;</c> of a type reference in both directions.
/// </summary>
public sealed class TypeRefTests
{
    [Theory]
    [InlineData("core.pressure@1", "core.pressure", 1)]
    [InlineData("vendor.acme.filter@12", "vendor.acme.filter", 12)]
    [InlineData("core.types.hsv@0", "core.types.hsv", 0)]
    [InlineData("x@2147483647", "x", int.MaxValue)]
    [InlineData("core.pressure@01", "core.pressure", 1)]
    public void Parse_ValidText_ReturnsNameAndMajor(string text, string expectedName, int expectedMajor)
    {
        // Arrange

        // Act
        var result = TypeRef.Parse(text);

        // Assert
        result.Should().Be(new TypeRef(expectedName, expectedMajor));
    }

    [Theory]
    [InlineData("core.pressure")]
    [InlineData("@1")]
    [InlineData(" @1")]
    [InlineData("core.pressure@")]
    [InlineData("core.pressure@-1")]
    [InlineData("core.pressure@+1")]
    [InlineData("core.pressure@1.2")]
    [InlineData("core.pressure@ 1")]
    [InlineData("core@pressure@1")]
    [InlineData("core.pressure@99999999999")]
    [InlineData("core.pressure@2147483648")]
    [InlineData(" core.pressure@1")]
    [InlineData("core.pressure @1")]
    [InlineData("core pressure@1")]
    [InlineData("core.pressure@1 ")]
    [InlineData("")]
    public void Parse_InvalidText_ThrowsFormatExceptionNamingTheInput(string text)
    {
        // Arrange

        // Act
        var act = () => TypeRef.Parse(text);

        // Assert
        act.Should().Throw<FormatException>().WithMessage($"*'{text}'*");
    }

    [Fact]
    public void Parse_Null_ThrowsArgumentNullException()
    {
        // Arrange

        // Act
        var act = () => TypeRef.Parse(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void TryParse_ValidText_ReturnsTrueAndTheValue()
    {
        // Arrange

        // Act
        var success = TypeRef.TryParse("core.onoff@1", out var result);

        // Assert
        success.Should().BeTrue();
        result.Should().Be(new TypeRef("core.onoff", 1));
    }

    [Theory]
    [InlineData("core.onoff")]
    [InlineData(null)]
    public void TryParse_InvalidText_ReturnsFalseAndTheDefault(string? text)
    {
        // Arrange

        // Act
        var success = TypeRef.TryParse(text, out var result);

        // Assert
        success.Should().BeFalse();
        result.Should().Be(default(TypeRef));
    }

    [Fact]
    public void ToString_Reference_ReturnsNameAtMajor()
    {
        // Arrange
        var reference = new TypeRef("core.level", 2);

        // Act
        var text = reference.ToString();

        // Assert
        text.Should().Be("core.level@2");
    }

    [Fact]
    public void Parse_ThroughIParsable_IgnoresTheFormatProvider()
    {
        // Arrange
        var provider = new System.Globalization.CultureInfo("de-DE");

        // Act
        var result = ParseGeneric<TypeRef>("core.level@2", provider);

        // Assert
        result.Should().Be(new TypeRef("core.level", 2));
    }

    private static T ParseGeneric<T>(string text, IFormatProvider provider)
        where T : IParsable<T> => T.Parse(text, provider);
}
