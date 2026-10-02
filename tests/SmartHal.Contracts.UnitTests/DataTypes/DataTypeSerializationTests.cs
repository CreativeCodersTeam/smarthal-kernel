using System.Text.Json;
using System.Text.Json.Nodes;
using AwesomeAssertions;
using SmartHal.Contracts.DataTypes;
using SmartHal.Contracts.Primitives;
using SmartHal.Contracts.Serialization;
using Xunit;

namespace SmartHal.Contracts.UnitTests.DataTypes;

/// <summary>
/// Verifies the polymorphic JSON form of the data types: every derived type carries its discriminator
/// <c>type</c> and survives a round trip.
/// </summary>
public sealed class DataTypeSerializationTests
{
    public static TheoryData<DataType, string> DataTypes =>
        new TheoryData<DataType, string>
    {
        { new BooleanType(), """{"type":"boolean"}""" },
        { new IntegerType(Minimum: 0, Maximum: 255), """{"type":"integer","minimum":0,"maximum":255}""" },
        { new NumberType("bar", 0, 16, 0.01), """{"type":"number","unit":"bar","minimum":0,"maximum":16,"step":0.01}""" },
        { new StringType(64, "^[a-z]+$"), """{"type":"string","maxLength":64,"pattern":"^[a-z]+$"}""" },
        { new EnumType(["online", "offline"]), """{"type":"enum","values":["online","offline"]}""" },
        { new TimestampType(), """{"type":"timestamp"}""" },
        { new DurationType(), """{"type":"duration"}""" },
        { new ArrayType(new BooleanType(), 8), """{"type":"array","items":{"type":"boolean"},"maxItems":8}""" },
        { new RefType(new TypeRef("core.types.hsv", 1)), """{"type":"ref","ref":"core.types.hsv@1"}""" }
    };

    [Theory]
    [MemberData(nameof(DataTypes))]
    public void Serialize_DataType_WritesDiscriminatorAndFields(DataType dataType, string expectedJson)
    {
        // Arrange

        // Act
        var json = JsonSerializer.Serialize(dataType, ContractsJson.Options);

        // Assert
        JsonNode.DeepEquals(JsonNode.Parse(json), JsonNode.Parse(expectedJson)).Should().BeTrue(json);
    }

    [Theory]
    [MemberData(nameof(DataTypes))]
    public void Deserialize_Json_ReadsTheDerivedType(DataType expected, string json)
    {
        // Arrange

        // Act
        var dataType = JsonSerializer.Deserialize<DataType>(json, ContractsJson.Options);

        // Assert
        // Records without members cannot be compared member by member, so the value is checked by writing it
        // back: the same JSON proves that every field was read.
        dataType.Should().BeOfType(expected.GetType());
        var written = JsonSerializer.Serialize(dataType, ContractsJson.Options);
        JsonNode.DeepEquals(JsonNode.Parse(written), JsonNode.Parse(json)).Should().BeTrue(written);
    }

    [Fact]
    public void RoundTrip_NestedObjectType_KeepsFieldsAndRequired()
    {
        // Arrange
        DataType hsv = new ObjectType(
            new Dictionary<string, DataType>
            {
                ["h"] = new NumberType("deg", 0, 360),
                ["s"] = new NumberType("%", 0, 100),
                ["v"] = new NumberType("%", 0, 100)
            },
            ["h", "s", "v"]);

        // Act
        var json = JsonSerializer.Serialize(hsv, ContractsJson.Options);
        var result = JsonSerializer.Deserialize<DataType>(json, ContractsJson.Options);

        // Assert
        JsonNode.Parse(json)!["fields"]!["h"]!["type"]!.GetValue<string>().Should().Be("number");
        result.Should().BeOfType<ObjectType>()
            .Which.Should().BeEquivalentTo(hsv, options => options.ComparingRecordsByMembers().PreferringRuntimeMemberTypes());
    }

    [Fact]
    public void Deserialize_UnknownDiscriminator_ThrowsJsonException()
    {
        // Arrange
        const string json = """{"type":"decimal"}""";

        // Act
        var act = () => JsonSerializer.Deserialize<DataType>(json, ContractsJson.Options);

        // Assert
        act.Should().Throw<JsonException>();
    }

    [Fact]
    public void RoundTrip_DataTypeDef_KeepsNameVersionAndType()
    {
        // Arrange
        var definition = new DataTypeDef("core.types.hsv", new TypeVersion(1, 0), new ArrayType(new NumberType()));

        // Act
        var json = JsonSerializer.Serialize(definition, ContractsJson.Options);
        var result = JsonSerializer.Deserialize<DataTypeDef>(json, ContractsJson.Options);

        // Assert
        json.Should().Be(
            """{"name":"core.types.hsv","version":"1.0","dataType":{"type":"array","items":{"type":"number"}}}""");
        result.Should().BeEquivalentTo(definition, options => options.ComparingRecordsByMembers().PreferringRuntimeMemberTypes());
    }
}
