using System.Text.Json;
using System.Text.Json.Nodes;
using AwesomeAssertions;
using SmartHal.Contracts.DataTypes;
using SmartHal.Contracts.Integration;
using SmartHal.Contracts.Integration.Transforms;
using SmartHal.Contracts.Primitives;
using SmartHal.Contracts.Schema;
using SmartHal.Contracts.Serialization;
using Xunit;

namespace SmartHal.Contracts.UnitTests.Schema;

/// <summary>
/// Verifies the JSON form of the schema layer: the alarm source discriminator <c>kind</c>, the snake_case enums of
/// the schema and complete round trips of a capability type, a migration and a device type.
/// </summary>
public sealed class SchemaSerializationTests
{
    [Theory]
    [InlineData("""{"kind":"device","event":"blocked"}""", typeof(DeviceAlarmSource))]
    [InlineData("""{"kind":"rule","property":"value","condition":"above"}""", typeof(RuleAlarmSource))]
    [InlineData("""{"kind":"rule","property":"status","condition":"equals"}""", typeof(RuleAlarmSource))]
    public void RoundTrip_AlarmSource_KeepsKindAndFields(string json, Type expectedType)
    {
        // Arrange

        // Act
        var source = JsonSerializer.Deserialize<AlarmSource>(json, ContractsJson.Options);
        var written = JsonSerializer.Serialize(source, ContractsJson.Options);

        // Assert
        source.Should().BeOfType(expectedType);
        JsonNode.DeepEquals(JsonNode.Parse(written), JsonNode.Parse(json)).Should().BeTrue(written);
    }

    [Fact]
    public void Serialize_CapabilityType_WritesTheSchemaEnumsAndDurationsInTheirContractForm()
    {
        // Arrange
        var capability = PressureCapability();

        // Act
        var node = JsonNode.Parse(JsonSerializer.Serialize(capability, ContractsJson.Options))!;

        // Assert
        node["name"]!.GetValue<string>().Should().Be("core.pressure");
        node["version"]!.GetValue<string>().Should().Be("1.0");
        var value = node["properties"]!["value"]!;
        value["category"]!.GetValue<string>().Should().Be("state");
        value["aggregation"]!.GetValue<string>().Should().Be("avg");
        value["history"]!["rawRetention"]!.GetValue<string>().Should().Be("P30D");
        value["history"]!["rollups"]![0]!["aggregates"]!.AsArray().Select(item => item!.GetValue<string>())
            .Should().Equal("min", "max", "avg", "last");
        value["history"]!["deadband"]!["minInterval"]!.GetValue<string>().Should().Be("PT10S");
        var calibrate = node["commands"]!["calibrate"]!;
        calibrate["completion"]!.GetValue<string>().Should().Be("result");
        calibrate["timeout"]!.GetValue<string>().Should().Be("PT30S");
        calibrate["requiredParameters"]!.AsArray().Select(item => item!.GetValue<string>()).Should().Equal("reference");
        var alarm = node["alarms"]!["highLimit"]!;
        alarm["severity"]!.GetValue<string>().Should().Be("major");
        alarm["source"]!["kind"]!.GetValue<string>().Should().Be("rule");
        alarm["parameters"]!["limit"]!.AsObject().ContainsKey("default").Should().BeFalse("a missing default is not written");
        alarm["parameters"]!["delay"]!["default"]!.GetValue<string>().Should().Be("PT10S");
    }

    [Fact]
    public void RoundTrip_CapabilityType_ProducesTheSameJson()
    {
        // Arrange
        var json = JsonSerializer.Serialize(PressureCapability(), ContractsJson.Options);

        // Act
        var result = JsonSerializer.Deserialize<CapabilityType>(json, ContractsJson.Options);

        // Assert
        result!.Alarms["highLimit"].Parameters!["limit"].Default.Should().BeNull();
        result.Commands["calibrate"].Result.Should().BeOfType<NumberType>();
        var written = JsonSerializer.Serialize(result, ContractsJson.Options);
        JsonNode.DeepEquals(JsonNode.Parse(written), JsonNode.Parse(json)).Should().BeTrue(written);
    }

    [Fact]
    public void Deserialize_CapabilityTypeWithoutCommands_ThrowsJsonException()
    {
        // Arrange
        const string json = """{"name":"core.contact","version":"1.0","properties":{},"events":{},"alarms":{}}""";

        // Act
        var act = () => JsonSerializer.Deserialize<CapabilityType>(json, ContractsJson.Options);

        // Assert
        act.Should().Throw<JsonException>();
    }

    [Fact]
    public void RoundTrip_CapabilityMigration_KeepsRemovedElementsAsNull()
    {
        // Arrange
        var migration = new CapabilityMigration(
            new TypeRef("core.level", 1),
            new TypeRef("core.level", 2),
            Properties: new Dictionary<string, string?> { ["level"] = "percent", ["legacy"] = null },
            Alarms: new Dictionary<string, string?> { ["tooLow"] = null });

        // Act
        var json = JsonSerializer.Serialize(migration, ContractsJson.Options);
        var result = JsonSerializer.Deserialize<CapabilityMigration>(json, ContractsJson.Options);

        // Assert
        json.Should().Be(
            """{"from":"core.level@1","to":"core.level@2","properties":{"level":"percent","legacy":null},"alarms":{"tooLow":null}}""");
        result!.Properties.Should().Contain("legacy", null);
        result.Alarms.Should().ContainKey("tooLow").WhoseValue.Should().BeNull();
    }

    [Fact]
    public void RoundTrip_DeviceType_KeepsChannelsSleepyAndBindingTemplates()
    {
        // Arrange
        var deviceType = new DeviceType(
            "acme.trv2",
            new TypeVersion(1, 0),
            "Acme",
            "TRV-2",
            [
                new ChannelTemplate("0", [new CapabilityTemplate("battery", new TypeRef("core.battery", 1))]),
                new ChannelTemplate(
                    "heizung",
                    [
                        new CapabilityTemplate("thermostat", new TypeRef("core.thermostat", 1)),
                        new CapabilityTemplate("temperature", new TypeRef("core.temperature", 1))
                    ],
                    new TypeRef("core.profile.thermostat", 1))
            ],
            new SleepyConfig(TimeSpan.FromMinutes(5)),
            [
                new BindingTemplate(
                    Protocols.Zigbee,
                    new Dictionary<string, DataType> { ["ieeeAddr"] = new StringType() },
                    [new Mapping("heizung/thermostat/setpoint", "${ieeeAddr}/0x0201/0x0012", [new ScaleStep(0.01)])])
            ]);

        // Act
        var json = JsonSerializer.Serialize(deviceType, ContractsJson.Options);
        var result = JsonSerializer.Deserialize<DeviceType>(json, ContractsJson.Options);

        // Assert
        var node = JsonNode.Parse(json)!;
        node["sleepy"]!["wakeInterval"]!.GetValue<string>().Should().Be("PT5M");
        node["channels"]![0]!.AsObject().ContainsKey("profile").Should().BeFalse();
        node["bindingTemplates"]![0]!["mappings"]![0]!["transform"]![0]!["fn"]!.GetValue<string>().Should().Be("scale");
        result!.Channels.Should().HaveCount(2);
        result.Channels[1].Profile.Should().Be(new TypeRef("core.profile.thermostat", 1));
        var written = JsonSerializer.Serialize(result, ContractsJson.Options);
        JsonNode.DeepEquals(JsonNode.Parse(written), node).Should().BeTrue(written);
    }

    [Fact]
    public void RoundTrip_ChannelProfile_KeepsCounts()
    {
        // Arrange
        var profile = new ChannelProfile(
            "core.profile.pumphydraulics",
            new TypeVersion(1, 0),
            [new ProfileCapability(new TypeRef("core.pressure", 1), Required: true, Min: 2, Max: 2)]);

        // Act
        var json = JsonSerializer.Serialize(profile, ContractsJson.Options);
        var result = JsonSerializer.Deserialize<ChannelProfile>(json, ContractsJson.Options);

        // Assert
        json.Should().Be(
            """{"name":"core.profile.pumphydraulics","version":"1.0","capabilities":[{"type":"core.pressure@1","required":true,"min":2,"max":2}]}""");
        result!.Capabilities.Should().ContainSingle().Which.Should().Be(profile.Capabilities[0]);
    }

    private static CapabilityType PressureCapability()
    {
        var bar = new NumberType("bar");

        return new CapabilityType(
            "core.pressure",
            new TypeVersion(1, 0),
            new Dictionary<string, PropertyDef>
            {
                ["value"] = new(
                    bar,
                    PropertyCategory.State,
                    History: new HistoryPolicy(
                        TimeSpan.FromDays(30),
                        [
                            new Rollup(
                                TimeSpan.FromMinutes(1),
                                [RollupAggregate.Min, RollupAggregate.Max, RollupAggregate.Avg, RollupAggregate.Last],
                                TimeSpan.FromDays(90))
                        ],
                        new Deadband(Absolute: 0.01, MinInterval: TimeSpan.FromSeconds(10))),
                    Aggregation: Aggregation.Avg)
            },
            new Dictionary<string, CommandDef>
            {
                ["calibrate"] = new(
                    Completion.Result,
                    TimeSpan.FromSeconds(30),
                    new Dictionary<string, DataType> { ["reference"] = bar },
                    Result: bar,
                    RequiredParameters: ["reference"])
            },
            new Dictionary<string, EventDef> { ["calibrated"] = new(Severity: Severity.Info) },
            new Dictionary<string, AlarmDef>
            {
                ["highLimit"] = new(
                    Severity.Major,
                    "Pressure above limit",
                    new RuleAlarmSource("value", AlarmCondition.Above),
                    new Dictionary<string, AlarmParameter>
                    {
                        ["limit"] = new(bar),
                        ["delay"] = new(new DurationType(), JsonValue.Create("PT10S")),
                        ["hysteresis"] = new(bar, JsonValue.Create(0))
                    })
            });
    }
}
