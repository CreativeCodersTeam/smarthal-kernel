using System.Text.Json;
using System.Text.Json.Nodes;
using AwesomeAssertions;
using SmartHal.Contracts.DataTypes;
using SmartHal.Contracts.Primitives;
using SmartHal.Contracts.Schema;
using SmartHal.Contracts.Serialization;
using Xunit;

namespace SmartHal.Contracts.UnitTests.Schema;

/// <summary>
/// Verifies the strict reading of the schema types and the details of their JSON form that the round trips do not
/// reach: unknown or missing discriminators, missing mandatory members, dictionary key spelling and null entries.
/// </summary>
public sealed class SchemaContractTests
{
    [Theory]
    [InlineData("""{"kind":"threshold","property":"value"}""")]
    [InlineData("""{"kind":"Rule","property":"value","condition":"above"}""")]
    [InlineData("""{"kind":"rule","property":"value"}""")]
    [InlineData("""{"kind":"rule","condition":"above"}""")]
    [InlineData("""{"kind":"rule","property":"value","condition":"greater"}""")]
    [InlineData("""{"kind":"device"}""")]
    [InlineData("""{"kind":"device","event":null}""")]
    public void Deserialize_InvalidAlarmSource_ThrowsJsonException(string json)
    {
        // Arrange

        // Act
        var act = () => JsonSerializer.Deserialize<AlarmSource>(json, ContractsJson.Options);

        // Assert
        act.Should().Throw<JsonException>();
    }

    [Fact]
    public void Deserialize_AlarmSourceWithoutKind_ThrowsJsonException()
    {
        // Arrange
        const string json = """{"property":"value","condition":"above"}""";

        // Act
        var act = () => JsonSerializer.Deserialize<AlarmSource>(json, ContractsJson.Options);

        // Assert
        act.Should().Throw<JsonException>().WithMessage("*discriminator*");
    }

    [Theory]
    [InlineData("AlarmDef", """{"message":"m","source":{"kind":"device","event":"e"}}""")]
    [InlineData("AlarmDef", """{"severity":"major","source":{"kind":"device","event":"e"}}""")]
    [InlineData("AlarmDef", """{"severity":"major","message":"m"}""")]
    [InlineData("PropertyDef", """{"category":"state"}""")]
    [InlineData("PropertyDef", """{"dataType":null,"category":"state"}""")]
    [InlineData("PropertyDef", """{"dataType":{"type":"boolean"}}""")]
    [InlineData("CommandDef", """{"timeout":"PT10S"}""")]
    [InlineData("CommandDef", """{"completion":"ack"}""")]
    [InlineData("ProfileCapability", """{"type":"core.onoff@1"}""")]
    [InlineData("ChannelTemplate", """{"key":0,"capabilities":[]}""")]
    [InlineData("HistoryPolicy", """{"rollups":[]}""")]
    [InlineData("Rollup", """{"interval":"PT1M","retention":"P90D"}""")]
    [InlineData("SleepyConfig", """{}""")]
    [InlineData("DeviceType", """{"name":"acme.x","version":"1.0","model":"X","channels":[]}""")]
    [InlineData("CapabilityType", """{"version":"1.0","properties":{},"commands":{},"events":{},"alarms":{}}""")]
    [InlineData("CapabilityType", """{"name":"core.x","version":"1.0","properties":null,"commands":{},"events":{},"alarms":{}}""")]
    public void Deserialize_SchemaTypeWithoutMandatoryMember_ThrowsJsonException(string typeName, string json)
    {
        // Arrange
        var type = typeof(CapabilityType).Assembly.GetType($"SmartHal.Contracts.Schema.{typeName}", throwOnError: true)!;

        // Act
        var act = () => JsonSerializer.Deserialize(json, type, ContractsJson.Options);

        // Assert
        act.Should().Throw<JsonException>();
    }

    [Fact]
    public void RoundTrip_DictionaryKeys_KeepTheirExactSpelling()
    {
        // Arrange
        var capability = new CapabilityType(
            "vendor.acme.filter",
            new TypeVersion(1, 0),
            new Dictionary<string, PropertyDef>
            {
                ["DifferentialPressure"] = new PropertyDef(new NumberType("bar"), PropertyCategory.Diagnostic),
                ["raw_value"] = new PropertyDef(new IntegerType(), PropertyCategory.Config, DeriveSetter: false, Feature: "raw")
            },
            new Dictionary<string, CommandDef>
            {
                ["flush"] = new CommandDef(Completion.Confirmed, TimeSpan.FromMinutes(2), Affects: ["DifferentialPressure"])
            },
            new Dictionary<string, EventDef> { ["clogged"] = new EventDef(new ObjectType(new Dictionary<string,
                DataType> { ["Level"] = new IntegerType() })) },
            new Dictionary<string, AlarmDef>(),
            ["raw"]);

        // Act
        var json = JsonSerializer.Serialize(capability, ContractsJson.Options);
        var result = JsonSerializer.Deserialize<CapabilityType>(json, ContractsJson.Options);

        // Assert
        var node = JsonNode.Parse(json)!;
        node["properties"]!.AsObject().Select(pair => pair.Key).Should().Equal("DifferentialPressure", "raw_value");
        node["properties"]!["raw_value"]!["category"]!.GetValue<string>().Should().Be("config");
        node["properties"]!["raw_value"]!["deriveSetter"]!.GetValue<bool>().Should().BeFalse("false is a value, not an omission");
        node["properties"]!["DifferentialPressure"]!["category"]!.GetValue<string>().Should().Be("diagnostic");
        node["commands"]!["flush"]!["completion"]!.GetValue<string>().Should().Be("confirmed");
        node["events"]!["clogged"]!["payload"]!["fields"]!.AsObject().ContainsKey("Level").Should().BeTrue();
        node["alarms"]!.AsObject().Count.Should().Be(0, "an empty mandatory map is written, not omitted");
        result!.Commands["flush"].Affects.Should().Equal("DifferentialPressure");
        result.Features.Should().Equal("raw");
    }

    [Theory]
    [InlineData("false")]
    [InlineData("""{"h":0}""")]
    [InlineData("[1,2]")]
    public void RoundTrip_AlarmParameterDefault_KeepsAnyJsonValue(string defaultJson)
    {
        // Arrange
        var json = $$"""{"dataType":{"type":"boolean"},"default":{{defaultJson}}}""";

        // Act
        var parameter = JsonSerializer.Deserialize<AlarmParameter>(json, ContractsJson.Options);
        var written = JsonSerializer.Serialize(parameter, ContractsJson.Options);

        // Assert
        JsonNode.DeepEquals(JsonNode.Parse(written), JsonNode.Parse(json)).Should().BeTrue(written);
    }

    [Fact]
    public void RoundTrip_AlarmParameterWithExplicitNullDefault_ReadsNullAndOmitsItOnWriting()
    {
        // Arrange
        const string json = """{"dataType":{"type":"number"},"default":null}""";

        // Act
        var parameter = JsonSerializer.Deserialize<AlarmParameter>(json, ContractsJson.Options);
        var written = JsonSerializer.Serialize(parameter, ContractsJson.Options);

        // Assert
        // An explicit null and a missing default mean the same: the instance has to set the value.
        parameter!.Default.Should().BeNull();
        written.Should().Be("""{"dataType":{"type":"number"}}""");
    }

    [Fact]
    public void Deserialize_NullEntryInAMapOrList_IsNotRejectedByTheSerializer()
    {
        // Arrange
        // System.Text.Json checks the nullability of members, not of generic type arguments, so null entries of
        // non-nullable element types pass. The contract validator reports them instead.
        const string capabilityJson =
            """{"name":"core.x","version":"1.0","properties":{"value":null},"commands":{},"events":{},"alarms":{}}""";
        const string deviceTypeJson = """{"name":"acme.x","version":"1.0","manufacturer":"Acme","model":"X","channels":[null]}""";

        // Act
        var capability = JsonSerializer.Deserialize<CapabilityType>(capabilityJson, ContractsJson.Options);
        var deviceType = JsonSerializer.Deserialize<DeviceType>(deviceTypeJson, ContractsJson.Options);

        // Assert
        capability!.Properties["value"].Should().BeNull();
        deviceType!.Channels.Should().ContainSingle().Which.Should().BeNull();
    }
}
