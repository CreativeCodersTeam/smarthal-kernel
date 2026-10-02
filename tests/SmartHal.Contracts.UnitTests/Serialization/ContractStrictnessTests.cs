using System.Text.Json;
using AwesomeAssertions;
using SmartHal.Contracts.DataTypes;
using SmartHal.Contracts.Primitives;
using SmartHal.Contracts.Serialization;
using Xunit;

namespace SmartHal.Contracts.UnitTests.Serialization;

/// <summary>
/// Verifies that <see cref="ContractsJson.Options"/> enforce the nullability contract on reading and that values
/// without a valid text form are not written.
/// </summary>
public sealed class ContractStrictnessTests
{
    // A copy is created once: CA1869 asks for cached options, and the copy is what the test examines.
    private static readonly JsonSerializerOptions CopiedOptions = new(ContractsJson.Options);

    [Theory]
    [InlineData("""{"type":"enum"}""")]
    [InlineData("""{"type":"array"}""")]
    [InlineData("""{"type":"ref"}""")]
    [InlineData("""{"type":"object"}""")]
    [InlineData("""{"type":"enum","values":null}""")]
    [InlineData("""{"type":"array","items":null}""")]
    [InlineData("""{"type":"object","fields":null}""")]
    public void Deserialize_MissingOrNullMandatoryMember_ThrowsJsonException(string json)
    {
        // Arrange

        // Act
        var act = () => JsonSerializer.Deserialize<DataType>(json, ContractsJson.Options);

        // Assert
        act.Should().Throw<JsonException>();
    }

    [Theory]
    [InlineData("""{"version":"1.0","dataType":{"type":"boolean"}}""")]
    [InlineData("""{"name":null,"version":"1.0","dataType":{"type":"boolean"}}""")]
    [InlineData("""{"name":"core.types.hsv","version":"1.0"}""")]
    public void Deserialize_DataTypeDefWithoutMandatoryMember_ThrowsJsonException(string json)
    {
        // Arrange

        // Act
        var act = () => JsonSerializer.Deserialize<DataTypeDef>(json, ContractsJson.Options);

        // Assert
        act.Should().Throw<JsonException>();
    }

    [Fact]
    public void Deserialize_OptionalMembersLeftOut_UsesTheirDefaults()
    {
        // Arrange
        const string json = """{"type":"number"}""";

        // Act
        var value = JsonSerializer.Deserialize<DataType>(json, ContractsJson.Options);

        // Assert
        value.Should().Be(new NumberType());
    }

    [Fact]
    public void Deserialize_MissingDiscriminator_ThrowsJsonException()
    {
        // Arrange
        const string json = """{"unit":"bar"}""";

        // Act
        var act = () => JsonSerializer.Deserialize<DataType>(json, ContractsJson.Options);

        // Assert
        act.Should().Throw<JsonException>().WithMessage("*discriminator*");
    }

    [Fact]
    public void Serialize_DerivedTypeThroughItsOwnStaticType_OmitsTheDiscriminator()
    {
        // Arrange
        var value = new NumberType("bar");

        // Act
        var json = JsonSerializer.Serialize(value, ContractsJson.Options);

        // Assert
        // Documented on DataType: the discriminator belongs to the base type.
        json.Should().Be("""{"unit":"bar"}""");
    }

    public static TheoryData<TypeRef> InvalidTypeRefs => new()
    {
        default(TypeRef),
        new TypeRef("a@b", 1),
        new TypeRef("core.onoff", -1),
        new TypeRef(" ", 1),
        new TypeRef("core onoff", 1)
    };

    [Theory]
    [MemberData(nameof(InvalidTypeRefs))]
    public void Serialize_TypeRefWithoutValidTextForm_ThrowsJsonException(TypeRef value)
    {
        // Arrange

        // Act
        var act = () => JsonSerializer.Serialize(value, ContractsJson.Options);

        // Assert
        act.Should().Throw<JsonException>();
    }

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(1, -1)]
    public void Serialize_TypeVersionWithNegativePart_ThrowsJsonException(int major, int minor)
    {
        // Arrange
        var value = new TypeVersion(major, minor);

        // Act
        var act = () => JsonSerializer.Serialize(value, ContractsJson.Options);

        // Assert
        act.Should().Throw<JsonException>();
    }

    [Fact]
    public void Serialize_DefaultTypeVersion_WritesZeroDotZero()
    {
        // Arrange

        // Act
        var json = JsonSerializer.Serialize(default(TypeVersion), ContractsJson.Options);

        // Assert
        json.Should().Be("\"0.0\"");
    }

    [Fact]
    public void Serialize_TypeVersionWithDefaultOptions_StillUsesTheStringForm()
    {
        // Arrange
        var value = new TypeVersion(2, 1);

        // Act
        var json = JsonSerializer.Serialize(value);

        // Assert
        json.Should().Be("\"2.1\"");
    }

    [Theory]
    [InlineData("\"2\"")]
    [InlineData("\"-1\"")]
    public void Deserialize_EnumAsNumericString_ThrowsJsonException(string value)
    {
        // Arrange
        var json = $$"""{"severity":{{value}}}""";

        // Act
        var act = () => JsonSerializer.Deserialize<SeverityHolder>(json, ContractsJson.Options);

        // Assert
        act.Should().Throw<JsonException>();
    }

    [Fact]
    public void Deserialize_ExplicitNullForNullableDuration_ReadsNull()
    {
        // Arrange
        const string json = """{"interval":null}""";

        // Act
        var value = JsonSerializer.Deserialize<DurationHolder>(json, ContractsJson.Options);

        // Assert
        value.Should().Be(new DurationHolder(null));
    }

    [Fact]
    public void RoundTrip_SingleTick_KeepsPrecision()
    {
        // Arrange
        var value = new DurationHolder(TimeSpan.FromTicks(1));

        // Act
        var json = JsonSerializer.Serialize(value, ContractsJson.Options);
        var result = JsonSerializer.Deserialize<DurationHolder>(json, ContractsJson.Options);

        // Assert
        json.Should().Be("""{"interval":"PT0.0000001S"}""");
        result.Should().Be(value);
    }

    [Fact]
    public void Copy_OfTheSharedOptions_IsWritableAndKeepsTheConverters()
    {
        // Arrange
        var copy = CopiedOptions;

        // Act
        // System.Text.Json locks options on first use, so writability is observed before serializing.
        var writableBeforeUse = !copy.IsReadOnly;
        var json = JsonSerializer.Serialize(new SeverityHolder(Severity.Critical), copy);

        // Assert
        writableBeforeUse.Should().BeTrue();
        json.Should().Be("""{"severity":"critical"}""");
    }

    private sealed record SeverityHolder(Severity Severity);

    private sealed record DurationHolder(TimeSpan? Interval);
}
