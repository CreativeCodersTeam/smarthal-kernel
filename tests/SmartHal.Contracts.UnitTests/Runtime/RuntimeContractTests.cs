using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using AwesomeAssertions;
using SmartHal.Contracts.Addressing;
using SmartHal.Contracts.Bus;
using SmartHal.Contracts.Discovery;
using SmartHal.Contracts.Primitives;
using SmartHal.Contracts.Runtime;
using SmartHal.Contracts.Schema;
using SmartHal.Contracts.Serialization;
using SmartHal.Contracts.Topology;
using Xunit;

namespace SmartHal.Contracts.UnitTests.Runtime;

/// <summary>
/// Verifies the strict reading of the instance, runtime and bus contracts, the handling of <see langword="null"/>
/// values and the wire names of their enums.
/// </summary>
public sealed class RuntimeContractTests
{
    private static readonly Address Pressure = new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "value");

    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-09-30T12:00:00+02:00", CultureInfo.InvariantCulture);

    [Theory]
    [InlineData("stateChanged")]
    [InlineData("state_changed")]
    [InlineData("AlarmRaised")]
    public void Deserialize_BusMessageWithUnknownKind_ThrowsJsonException(string kind)
    {
        // Arrange
        var json = $$"""{"kind":"{{kind}}","seq":1}""";

        // Act
        var act = () => JsonSerializer.Deserialize<BusMessage>(json, ContractsJson.Options);

        // Assert
        act.Should().Throw<JsonException>();
    }

    [Theory]
    [InlineData("seq")]
    [InlineData("state")]
    public void Deserialize_StateChangedWithoutMandatoryMember_ThrowsJsonException(string member)
    {
        // Arrange
        var node = JsonNode.Parse(JsonSerializer.Serialize<BusMessage>(StateChangedMessage(8.4), ContractsJson.Options))!.AsObject();
        node.Remove(member);

        // Act
        var act = () => JsonSerializer.Deserialize<BusMessage>(node.ToJsonString(), ContractsJson.Options);

        // Assert
        act.Should().Throw<JsonException>();
    }

    [Fact]
    public void Deserialize_PropertyStateWithoutSeq_ThrowsJsonException()
    {
        // Arrange
        var node = JsonNode.Parse(JsonSerializer.Serialize(StateChangedMessage(1).State, ContractsJson.Options))!.AsObject();
        node.Remove("seq");

        // Act
        var act = () => JsonSerializer.Deserialize<PropertyState>(node.ToJsonString(), ContractsJson.Options);

        // Assert
        act.Should().Throw<JsonException>("a missing sequence number must not default to 0 and break the ordering");
    }

    [Fact]
    public void RoundTrip_StateChangedWithExplicitNullValue_KeepsTheNullValueAndOmitsTheMissingPrevious()
    {
        // Arrange
        BusMessage message = new StateChanged(
            4,
            new PropertyState(Pressure, null, Now, new Quality(QualityLevel.Bad, QualityReasons.InvalidValue), StateOrigin.Device, 4));

        // Act
        var json = JsonSerializer.Serialize(message, ContractsJson.Options);
        var result = JsonSerializer.Deserialize<BusMessage>(json, ContractsJson.Options);

        // Assert
        var node = JsonNode.Parse(json)!.AsObject();
        node["state"]!.AsObject().ContainsKey("value").Should().BeTrue();
        node.ContainsKey("previous").Should().BeFalse("without a previous state neither value nor quality is written");
        node.ContainsKey("previousQuality").Should().BeFalse();
        var stateChanged = result.Should().BeOfType<StateChanged>().Subject;
        stateChanged.State.Value.Should().BeNull();
        stateChanged.PreviousQuality.Should().BeNull();
    }

    public static TheoryData<string> NonScalarValues => ["\"on\"", "true", """{"h":120,"s":50,"v":80}""", "[1,2,3]"];

    [Theory]
    [MemberData(nameof(NonScalarValues))]
    public void RoundTrip_PropertyStateWithAnyJsonValue_KeepsTheValue(string valueJson)
    {
        // Arrange
        var state = new PropertyState(Pressure, JsonNode.Parse(valueJson), Now, new Quality(QualityLevel.Good), StateOrigin.Device, 1);

        // Act
        var json = JsonSerializer.Serialize(state, ContractsJson.Options);
        var result = JsonSerializer.Deserialize<PropertyState>(json, ContractsJson.Options);

        // Assert
        JsonNode.DeepEquals(result!.Value, JsonNode.Parse(valueJson)).Should().BeTrue(json);
        result.ReceivedTs.Offset.Should().Be(TimeSpan.FromHours(2), "the offset of a timestamp is kept");
    }

    [Fact]
    public void RoundTrip_FailedChildInvocation_KeepsParentResultAndError()
    {
        // Arrange
        var parentId = Guid.NewGuid();
        var invocation = new CommandInvocation(
            Guid.NewGuid(),
            "idem-7",
            Pressure with { Element = "resetFault" },
            new Dictionary<string, JsonNode?> { ["force"] = null },
            new Issuer(IssuerKind.System, "fanout"),
            CommandStatus.Failed,
            Now,
            Now.AddSeconds(10),
            [new StatusTransition(CommandStatus.Pending, Now), new StatusTransition(CommandStatus.Failed, Now.AddSeconds(3))],
            parentId,
            JsonNode.Parse("""{"attempts":2}"""),
            new CommandError("device_rejected", "The device rejected the reset."));

        // Act
        var json = JsonSerializer.Serialize(invocation, ContractsJson.Options);
        var result = JsonSerializer.Deserialize<CommandInvocation>(json, ContractsJson.Options);

        // Assert
        var node = JsonNode.Parse(json)!;
        node["status"]!.GetValue<string>().Should().Be("failed");
        node["issuer"]!["kind"]!.GetValue<string>().Should().Be("system");
        node["parameters"]!.AsObject().ContainsKey("force").Should().BeTrue("null entries of a dictionary are kept");
        result!.ParentId.Should().Be(parentId);
        result.Error.Should().Be(new CommandError("device_rejected", "The device rejected the reset."));
        result.Result!["attempts"]!.GetValue<int>().Should().Be(2);
        JsonNode.DeepEquals(JsonNode.Parse(JsonSerializer.Serialize(result, ContractsJson.Options)), node).Should().BeTrue();
    }

    [Theory]
    [InlineData("idempotencyKey")]
    [InlineData("issuer")]
    [InlineData("deadline")]
    [InlineData("transitions")]
    [InlineData("status")]
    public void Deserialize_CommandInvocationWithoutMandatoryMember_ThrowsJsonException(string member)
    {
        // Arrange
        var invocation = new CommandInvocation(
            Guid.NewGuid(), "idem", Pressure, new Dictionary<string, JsonNode?>(), new Issuer(IssuerKind.User, "u"),
            CommandStatus.Pending, Now, Now.AddSeconds(5), []);
        var node = JsonNode.Parse(JsonSerializer.Serialize(invocation, ContractsJson.Options))!.AsObject();
        node.Remove(member);

        // Act
        var act = () => JsonSerializer.Deserialize<CommandInvocation>(node.ToJsonString(), ContractsJson.Options);

        // Assert
        act.Should().Throw<JsonException>();
    }

    [Theory]
    [InlineData("virtual")]
    [InlineData("lifecycle")]
    [InlineData("channels")]
    public void Deserialize_DeviceWithoutMandatoryMember_ThrowsJsonException(string member)
    {
        // Arrange
        var device = new Device(Guid.NewGuid(), "d", "D", false, DeviceLifecycle.Provisioned, []);
        var node = JsonNode.Parse(JsonSerializer.Serialize(device, ContractsJson.Options))!.AsObject();
        node.Remove(member);

        // Act
        var act = () => JsonSerializer.Deserialize<Device>(node.ToJsonString(), ContractsJson.Options);

        // Assert
        act.Should().Throw<JsonException>();
    }

    [Theory]
    [InlineData("bindingId")]
    [InlineData("features")]
    [InlineData("version")]
    public void Deserialize_CapabilityWithoutMandatoryMember_ThrowsJsonException(string member)
    {
        // Arrange
        var capability = new Capability(Guid.NewGuid(), "c", new TypeRef("core.onoff", 1), new TypeVersion(1, 0), [], Guid.NewGuid());
        var node = JsonNode.Parse(JsonSerializer.Serialize(capability, ContractsJson.Options))!.AsObject();
        node.Remove(member);

        // Act
        var act = () => JsonSerializer.Deserialize<Capability>(node.ToJsonString(), ContractsJson.Options);

        // Assert
        act.Should().Throw<JsonException>();
    }

    [Fact]
    public void RoundTrip_CapabilityWithHistoryOverride_KeepsThePolicy()
    {
        // Arrange
        var capability = new Capability(
            Guid.NewGuid(),
            "druckseite",
            new TypeRef("core.pressure", 1),
            new TypeVersion(1, 0),
            [],
            Guid.NewGuid(),
            new Dictionary<string, HistoryPolicy> { ["value"] = new(TimeSpan.FromDays(7), Deadband: new Deadband(Relative: 0.05)) });

        // Act
        var json = JsonSerializer.Serialize(capability, ContractsJson.Options);
        var result = JsonSerializer.Deserialize<Capability>(json, ContractsJson.Options);

        // Assert
        JsonNode.Parse(json)!["historyOverrides"]!["value"]!["rawRetention"]!.GetValue<string>().Should().Be("P7D");
        result!.HistoryOverrides!["value"].Deadband.Should().Be(new Deadband(Relative: 0.05));
    }

    [Fact]
    public void RoundTrip_EventOccurrenceWithObjectPayload_KeepsThePayload()
    {
        // Arrange
        var occurrence = new EventOccurrence(Guid.NewGuid(), Pressure with { Element = "pressed" }, Now, Now,
            JsonNode.Parse("""{"type":"double"}"""));

        // Act
        var json = JsonSerializer.Serialize(occurrence, ContractsJson.Options);
        var result = JsonSerializer.Deserialize<EventOccurrence>(json, ContractsJson.Options);

        // Assert
        JsonNode.Parse(json)!.AsObject().ContainsKey("severity").Should().BeFalse();
        result!.Payload!["type"]!.GetValue<string>().Should().Be("double");
    }

    public static TheoryData<Enum, string> EnumWireNames => new()
    {
        { DeviceLifecycle.Provisioned, "provisioned" },
        { DeviceLifecycle.Decommissioned, "decommissioned" },
        { LocationKind.Site, "site" },
        { LocationKind.Floor, "floor" },
        { LocationKind.Room, "room" },
        { LocationKind.Zone, "zone" },
        { DiscoveryStatus.Approved, "approved" },
        { DiscoveryStatus.Rejected, "rejected" },
        { DiscoveryStatus.Ignored, "ignored" },
        { AlarmState.ClearedUnacked, "cleared_unacked" },
        { AlarmState.Cleared, "cleared" },
        { AlarmTrigger.Device, "device" },
        { IssuerKind.User, "user" },
        { StateOrigin.Virtual, "virtual" },
        { CommandStatus.Sent, "sent" },
        { CommandStatus.Completed, "completed" }
    };

    [Theory]
    [MemberData(nameof(EnumWireNames))]
    public void Serialize_EnumMember_WritesItsWireName(Enum value, string expected)
    {
        // Arrange

        // Act
        var json = JsonSerializer.Serialize(value, value.GetType(), ContractsJson.Options);

        // Assert
        json.Should().Be($"\"{expected}\"");
    }

    [Fact]
    public void QualityReasons_AllReasons_HaveTheirWireNames()
    {
        // Arrange
        string[] reasons =
        [
            QualityReasons.UpstreamOffline, QualityReasons.SourceOffline, QualityReasons.InvalidValue,
            QualityReasons.Stale, QualityReasons.MemberBad
        ];

        // Act

        // Assert
        reasons.Should().Equal("upstream_offline", "source_offline", "invalid_value", "stale", "member_bad");
    }

    [Fact]
    public void Deserialize_DiscoveryResultWithoutParameters_ThrowsJsonException()
    {
        // Arrange
        var result = new DiscoveryResult(Guid.NewGuid(), Guid.NewGuid(), "addr", new Dictionary<string, JsonNode?>(),
            DiscoveryStatus.New, Now);
        var node = JsonNode.Parse(JsonSerializer.Serialize(result, ContractsJson.Options))!.AsObject();
        node.Remove("parameters");

        // Act
        var act = () => JsonSerializer.Deserialize<DiscoveryResult>(node.ToJsonString(), ContractsJson.Options);

        // Assert
        act.Should().Throw<JsonException>();
    }

    private static StateChanged StateChangedMessage(double value) =>
        new(1, new PropertyState(Pressure, value, Now, new Quality(QualityLevel.Good), StateOrigin.Device, 1), 8.0);
}
