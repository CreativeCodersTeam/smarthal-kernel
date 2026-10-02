using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using AwesomeAssertions;
using SmartHal.Contracts.Addressing;
using SmartHal.Contracts.Bus;
using SmartHal.Contracts.Discovery;
using SmartHal.Contracts.Primitives;
using SmartHal.Contracts.Runtime;
using SmartHal.Contracts.Serialization;
using SmartHal.Contracts.Topology;
using Xunit;

namespace SmartHal.Contracts.UnitTests.Runtime;

/// <summary>
/// Verifies the JSON form of the instance, runtime and bus contracts: the bus discriminator <c>kind</c>, the
/// always-written state value, the snake_case lifecycle enums and complete round trips.
/// </summary>
public sealed class RuntimeSerializationTests
{
    private static readonly Address Pressure = new Address(Guid.Parse("11111111-1111-1111-1111-111111111111"),
        Guid.Parse("22222222-2222-2222-2222-222222222222"), Guid.Parse("33333333-3333-3333-3333-333333333333"), "value");

    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-09-30T12:00:00Z", CultureInfo.InvariantCulture);

    [Fact]
    public void Serialize_PropertyStateWithoutValue_WritesValueAsNull()
    {
        // Arrange
        var state = new PropertyState(Pressure, null, Now, new Quality(QualityLevel.Bad, QualityReasons.InvalidValue),
            StateOrigin.Device, 7);

        // Act
        var node = JsonNode.Parse(JsonSerializer.Serialize(state, ContractsJson.Options))!.AsObject();

        // Assert
        node.ContainsKey("value").Should().BeTrue("a missing value is a statement of its own");
        node["value"].Should().BeNull();
        node.ContainsKey("sourceTs").Should().BeFalse();
        node["quality"]!["level"]!.GetValue<string>().Should().Be("bad");
        node["quality"]!["reason"]!.GetValue<string>().Should().Be("invalid_value");
        node["origin"]!.GetValue<string>().Should().Be("device");
    }

    [Fact]
    public void Deserialize_PropertyStateWithoutValueMember_ThrowsJsonException()
    {
        // Arrange
        var json = JsonNode.Parse(JsonSerializer.Serialize(
            new PropertyState(Pressure, 8.2, Now, new Quality(QualityLevel.Good), StateOrigin.Device, 1),
            ContractsJson.Options))!.AsObject();
        json.Remove("value");

        // Act
        var act = () => JsonSerializer.Deserialize<PropertyState>(json.ToJsonString(), ContractsJson.Options);

        // Assert
        act.Should().Throw<JsonException>();
    }

    public static TheoryData<BusMessage, string> BusMessages =>
        new TheoryData<BusMessage, string>
    {
        {
            new StateChanged(
                5,
                new PropertyState(Pressure, 8.4, Now, new Quality(QualityLevel.Good), StateOrigin.Device, 5, Now.AddSeconds(-1)),
                8.1,
                new Quality(QualityLevel.Uncertain, QualityReasons.SourceOffline)),
            "StateChanged"
        },
        {
            new EventOccurred(9, new EventOccurrence(Guid.NewGuid(), Pressure with { Element = "highLimit" }, Now, Now,
                Severity: Severity.Major)),
            "EventOccurred"
        },
        {
            new CommandUpdated(
                3,
                new CommandInvocation(
                    Guid.NewGuid(),
                    "idem-1",
                    Pressure with { Element = "setLevel" },
                    new Dictionary<string, JsonNode?> { ["level"] = 50 },
                    new Issuer(IssuerKind.Automation, "flur-abend"),
                    CommandStatus.Cancelled,
                    Now,
                    Now.AddSeconds(30),
                    [new StatusTransition(CommandStatus.Pending, Now), new StatusTransition(CommandStatus.Cancelled, Now.AddSeconds(1))])),
            "CommandUpdated"
        }
    };

    [Theory]
    [MemberData(nameof(BusMessages))]
    public void RoundTrip_BusMessage_KeepsKindSeqAndPayload(BusMessage message, string expectedKind)
    {
        // Arrange

        // Act
        var json = JsonSerializer.Serialize(message, ContractsJson.Options);
        var result = JsonSerializer.Deserialize<BusMessage>(json, ContractsJson.Options);

        // Assert
        var node = JsonNode.Parse(json)!;
        node["kind"]!.GetValue<string>().Should().Be(expectedKind);
        node["seq"]!.GetValue<long>().Should().Be(message.Seq);
        result.Should().BeOfType(message.GetType());
        result.Seq.Should().Be(message.Seq);
        JsonNode.DeepEquals(JsonNode.Parse(JsonSerializer.Serialize(result, ContractsJson.Options)), node).Should().BeTrue(json);
    }

    [Fact]
    public void Serialize_StateChangedWithPreviousQuality_WritesPreviousValueAndQuality()
    {
        // Arrange
        BusMessage message = new StateChanged(
            2,
            new PropertyState(Pressure, 8.4, Now, new Quality(QualityLevel.Good), StateOrigin.Device, 2),
            8.4,
            new Quality(QualityLevel.Uncertain, QualityReasons.UpstreamOffline));

        // Act
        var node = JsonNode.Parse(JsonSerializer.Serialize(message, ContractsJson.Options))!;

        // Assert
        node["previous"]!.GetValue<double>().Should().Be(8.4);
        node["previousQuality"]!["level"]!.GetValue<string>().Should().Be("uncertain");
        node["previousQuality"]!["reason"]!.GetValue<string>().Should().Be("upstream_offline");
    }

    [Theory]
    [InlineData(CommandStatus.Pending, "pending")]
    [InlineData(CommandStatus.Acked, "acked")]
    [InlineData(CommandStatus.Partial, "partial")]
    [InlineData(CommandStatus.Timeout, "timeout")]
    [InlineData(CommandStatus.Skipped, "skipped")]
    public void Serialize_CommandStatus_WritesSnakeCaseName(CommandStatus status, string expected)
    {
        // Arrange

        // Act
        var json = JsonSerializer.Serialize(status, ContractsJson.Options);

        // Assert
        json.Should().Be($"\"{expected}\"");
    }

    [Fact]
    public void RoundTrip_AlarmInstance_KeepsStateTransitionsAndShelving()
    {
        // Arrange
        var alarm = new AlarmInstance(
            Guid.NewGuid(),
            Pressure with { Element = "highLimit" },
            AlarmTrigger.Rule,
            Severity.Major,
            AlarmState.ActiveAcked,
            [new AlarmTransition(AlarmState.ActiveUnacked, Now), new AlarmTransition(AlarmState.ActiveAcked, Now.AddMinutes(2),
                "schichtleiter")],
            Now.AddHours(1));

        // Act
        var json = JsonSerializer.Serialize(alarm, ContractsJson.Options);
        var result = JsonSerializer.Deserialize<AlarmInstance>(json, ContractsJson.Options);

        // Assert
        var node = JsonNode.Parse(json)!;
        node["state"]!.GetValue<string>().Should().Be("active_acked");
        node["trigger"]!.GetValue<string>().Should().Be("rule");
        node["transitions"]![0]!["state"]!.GetValue<string>().Should().Be("active_unacked");
        node["transitions"]![0]!.AsObject().ContainsKey("by").Should().BeFalse();
        result!.Transitions[1].By.Should().Be("schichtleiter");
        result.ShelvedUntil.Should().Be(Now.AddHours(1));
    }

    [Fact]
    public void RoundTrip_Device_KeepsChannelsCapabilitiesAndInstanceOverrides()
    {
        // Arrange
        var device = new Device(
            Guid.NewGuid(),
            "halle2.pumpe3",
            "Pumpe 3",
            Virtual: false,
            DeviceLifecycle.Active,
            [
                new Channel(Guid.NewGuid(), "0", []),
                new Channel(
                    Guid.NewGuid(),
                    "hydraulik",
                    [
                        new Capability(
                            Guid.NewGuid(),
                            "druckseite",
                            new TypeRef("core.pressure", 1),
                            new TypeVersion(1, 2),
                            [],
                            Guid.NewGuid(),
                            AlarmParameters: new Dictionary<string, IReadOnlyDictionary<string, JsonNode?>>
                            {
                                ["highLimit"] = new Dictionary<string, JsonNode?> { ["limit"] = 8, ["delay"] = "PT10S" }
                            },
                            Aliases: ["druck2"],
                            Tags: new Dictionary<string, string> { ["gewerk"] = "hlk" })
                    ])
            ],
            new TypeRef("acme.pump", 1),
            ConnectedVia: Guid.NewGuid());

        // Act
        var json = JsonSerializer.Serialize(device, ContractsJson.Options);
        var result = JsonSerializer.Deserialize<Device>(json, ContractsJson.Options);

        // Assert
        var node = JsonNode.Parse(json)!;
        node["lifecycle"]!.GetValue<string>().Should().Be("active");
        node["typeRef"]!.GetValue<string>().Should().Be("acme.pump@1");
        node["virtual"]!.GetValue<bool>().Should().BeFalse();
        var capability = node["channels"]![1]!["capabilities"]![0]!;
        capability["version"]!.GetValue<string>().Should().Be("1.2");
        capability["features"]!.AsArray().Should().BeEmpty();
        capability["alarmParameters"]!["highLimit"]!["limit"]!.GetValue<int>().Should().Be(8);
        result!.Channels[1].Capabilities[0].Tags.Should().Contain("gewerk", "hlk");
        JsonNode.DeepEquals(JsonNode.Parse(JsonSerializer.Serialize(result, ContractsJson.Options)), node).Should().BeTrue();
    }

    [Fact]
    public void RoundTrip_DiscoveryResult_KeepsStatusParametersAndSuggestedType()
    {
        // Arrange
        var result = new DiscoveryResult(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "0x00158d0001a2b3c4",
            new Dictionary<string, JsonNode?> { ["ieeeAddr"] = "0x00158d0001a2b3c4" },
            DiscoveryStatus.New,
            Now,
            new TypeRef("acme.trv2", 1));

        // Act
        var json = JsonSerializer.Serialize(result, ContractsJson.Options);
        var read = JsonSerializer.Deserialize<DiscoveryResult>(json, ContractsJson.Options);

        // Assert
        JsonNode.Parse(json)!["status"]!.GetValue<string>().Should().Be("new");
        read!.SuggestedType.Should().Be(new TypeRef("acme.trv2", 1));
        read.Parameters["ieeeAddr"]!.GetValue<string>().Should().Be("0x00158d0001a2b3c4");
    }

    [Fact]
    public void Serialize_Location_WritesKindAndOmitsMissingParent()
    {
        // Arrange
        var location = new Location(Guid.NewGuid(), "halle2", "Halle 2", LocationKind.Building);

        // Act
        var node = JsonNode.Parse(JsonSerializer.Serialize(location, ContractsJson.Options))!.AsObject();

        // Assert
        node["kind"]!.GetValue<string>().Should().Be("building");
        node.ContainsKey("parentId").Should().BeFalse();
        location.Should().BeAssignableTo<IIdentified>();
    }
}
