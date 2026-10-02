using System.Text.Json;
using AwesomeAssertions;
using SmartHal.Contracts.Primitives;
using SmartHal.Contracts.Serialization;
using Xunit;

namespace SmartHal.Contracts.UnitTests.Serialization;

/// <summary>
/// Verifies that type references and type versions travel as JSON strings and that anything else is rejected.
/// </summary>
public sealed class TypeRefConverterTests
{
    [Fact]
    public void Serialize_TypeRefAndVersion_WritesStrings()
    {
        // Arrange
        var value = new Holder(new TypeRef("core.pressure", 1), new TypeVersion(1, 2));

        // Act
        var json = JsonSerializer.Serialize(value, ContractsJson.Options);

        // Assert
        json.Should().Be("""{"ref":"core.pressure@1","version":"1.2"}""");
    }

    [Fact]
    public void Deserialize_Strings_ReadsTypeRefAndVersion()
    {
        // Arrange
        const string json = """{"ref":"vendor.acme.filter@3","version":"3.4"}""";

        // Act
        var value = JsonSerializer.Deserialize<Holder>(json, ContractsJson.Options);

        // Assert
        value.Should().Be(new Holder(new TypeRef("vendor.acme.filter", 3), new TypeVersion(3, 4)));
    }

    [Theory]
    [InlineData("""{"ref":"core.pressure","version":"1.0"}""")]
    [InlineData("""{"ref":null,"version":"1.0"}""")]
    [InlineData("""{"ref":1,"version":"1.0"}""")]
    [InlineData("""{"ref":"core.pressure@1","version":"1"}""")]
    [InlineData("""{"ref":"core.pressure@1","version":null}""")]
    [InlineData("""{"ref":"core.pressure@1","version":1.0}""")]
    public void Deserialize_InvalidValue_ThrowsJsonException(string json)
    {
        // Arrange

        // Act
        var act = () => JsonSerializer.Deserialize<Holder>(json, ContractsJson.Options);

        // Assert
        act.Should().Throw<JsonException>();
    }

    [Fact]
    public void Serialize_WithDefaultOptions_StillUsesTheStringForm()
    {
        // Arrange
        var value = new TypeRef("core.onoff", 1);

        // Act
        var json = JsonSerializer.Serialize(value);

        // Assert
        json.Should().Be("\"core.onoff@1\"");
    }

    private sealed record Holder(TypeRef Ref, TypeVersion Version);
}
