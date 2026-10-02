using System.Text.Json;
using System.Text.Json.Nodes;
using AwesomeAssertions;
using SmartHal.Contracts.Addressing;
using SmartHal.Contracts.Integration;
using SmartHal.Contracts.Integration.Transforms;
using SmartHal.Contracts.Integration.Virtual;
using SmartHal.Contracts.Primitives;
using SmartHal.Contracts.Serialization;
using Xunit;

namespace SmartHal.Contracts.UnitTests.Integration;

/// <summary>
/// Verifies the remaining JSON contract of the integration types: reading the documented mapping example, protocol
/// bindings, adapters, null entries in value collections and the strictness of the substitutions.
/// </summary>
public sealed class IntegrationContractTests
{
    [Fact]
    public void Deserialize_MappingExampleOfTheFormalSchema_ReadsEveryField()
    {
        // Arrange
        // Verbatim from docs/Formales Schema (C#).md, section "API zur Automationsschicht", example.
        const string json =
            """{"target":"hydraulik/druckseite/value","address":"unit=${slaveId};hr=40002","transform":[{"fn":"scale","factor":0.01}],"invalidValues":[65535],"pollInterval":"PT1S"}""";

        // Act
        var mapping = JsonSerializer.Deserialize<Mapping>(json, ContractsJson.Options);

        // Assert
        mapping!.Target.Should().Be("hydraulik/druckseite/value");
        mapping.Address.Should().Be("unit=${slaveId};hr=40002");
        mapping.Transform.Should().ContainSingle().Which.Should().Be(new ScaleStep(0.01));
        mapping.InvalidValues.Should().ContainSingle().Which!.GetValue<int>().Should().Be(65535);
        mapping.PollInterval.Should().Be(TimeSpan.FromSeconds(1));
        mapping.Stateful.Should().BeNull();
    }

    [Fact]
    public void RoundTrip_ProtocolBinding_KeepsAdapterTemplateAndOverrides()
    {
        // Arrange
        var adapterId = Guid.NewGuid();
        var binding = new Binding(
            Guid.NewGuid(),
            Guid.NewGuid(),
            BindingKind.Protocol,
            new Dictionary<string, JsonNode?> { ["slaveId"] = 3 },
            new BindingStatus(BindingState.Offline, new DateTimeOffset(2026, 9, 30, 14, 0, 0, TimeSpan.FromHours(2))),
            adapterId,
            new TemplateRef(new TypeRef("acme.pump", 1), Protocols.Modbus),
            [
                new Mapping(
                    "motor/runstate/state",
                    "unit=${slaveId};hr=40010",
                    [new ScaleStep(2), new OffsetStep(-1), new EnumMapStep(new Dictionary<string, JsonNode?> { ["0"] = "stopped" })],
                    PollInterval: TimeSpan.FromSeconds(60))
            ]);

        // Act
        var json = JsonSerializer.Serialize(binding, ContractsJson.Options);
        var result = JsonSerializer.Deserialize<Binding>(json, ContractsJson.Options);

        // Assert
        var node = JsonNode.Parse(json)!;
        node["kind"]!.GetValue<string>().Should().Be("protocol");
        node["template"]!["deviceType"]!.GetValue<string>().Should().Be("acme.pump@1");
        node["status"]!["lastSeen"]!.GetValue<string>().Should().Be("2026-09-30T14:00:00+02:00");
        node.AsObject().ContainsKey("internal").Should().BeFalse();
        result!.AdapterId.Should().Be(adapterId);
        result.Status.LastSeen!.Value.Offset.Should().Be(TimeSpan.FromHours(2));
        result.Overrides.Should().ContainSingle().Which.Transform!.Select(step => step.GetType())
            .Should().Equal(typeof(ScaleStep), typeof(OffsetStep), typeof(EnumMapStep));
        JsonNode.DeepEquals(JsonNode.Parse(JsonSerializer.Serialize(result, ContractsJson.Options)), node).Should().BeTrue();
    }

    [Fact]
    public void RoundTrip_Adapter_KeepsConnectionsDiscoveryAndStatus()
    {
        // Arrange
        var adapterId = Guid.NewGuid();
        var adapter = new Adapter(
            adapterId,
            "zigbee.haus",
            Protocols.Zigbee,
            [
                new Connection(Guid.NewGuid(), adapterId, "/dev/ttyUSB0", ConnectionStatus.Connected),
                new Connection(Guid.NewGuid(), adapterId, "/dev/ttyUSB1", ConnectionStatus.Disconnected)
            ],
            new DiscoverySettings(true),
            AdapterStatus.Running);

        // Act
        var json = JsonSerializer.Serialize(adapter, ContractsJson.Options);
        var result = JsonSerializer.Deserialize<Adapter>(json, ContractsJson.Options);

        // Assert
        var node = JsonNode.Parse(json)!;
        node["status"]!.GetValue<string>().Should().Be("running");
        node["connections"]![1]!["status"]!.GetValue<string>().Should().Be("disconnected");
        node["discovery"]!["enabled"]!.GetValue<bool>().Should().BeTrue();
        result!.Connections.Select(connection => connection.Endpoint).Should().Equal("/dev/ttyUSB0", "/dev/ttyUSB1");
        JsonNode.DeepEquals(JsonNode.Parse(JsonSerializer.Serialize(result, ContractsJson.Options)), node).Should().BeTrue();
    }

    [Theory]
    [InlineData("""{"id":"00000000-0000-0000-0000-000000000001","key":"a","protocol":"zigbee","connections":[],"status":"running"}""")]
    [InlineData("""{"id":"00000000-0000-0000-0000-000000000001","key":"a","protocol":"zigbee","discovery":{"enabled":true},"status":"running"}""")]
    public void Deserialize_AdapterWithoutMandatoryMember_ThrowsJsonException(string json)
    {
        // Arrange

        // Act
        var act = () => JsonSerializer.Deserialize<Adapter>(json, ContractsJson.Options);

        // Assert
        act.Should().Throw<JsonException>();
    }

    [Fact]
    public void Serialize_KeyAddressThroughElementRef_WritesKeyDiscriminatorAndCapabilityKey()
    {
        // Arrange
        ElementRef address = new KeyAddress("halle2.pumpe3", "hydraulik", "druckseite", "value");

        // Act
        var json = JsonSerializer.Serialize(address, ContractsJson.Options);

        // Assert
        json.Should().Be("""{"by":"key","device":"halle2.pumpe3","channel":"hydraulik","capability":"druckseite","element":"value"}""");
    }

    [Theory]
    [InlineData("""{"pattern":"fixed","use":{"command":"on"},"memberType":"core.onoff@1"}""")]
    [InlineData("""{"pattern":"fixed","use":{"command":"on"},"command":null,"memberType":"core.onoff@1"}""")]
    [InlineData("""{"pattern":"fixed","use":{"command":"on"},"command":"toggle"}""")]
    [InlineData("""{"pattern":"fixed","command":"toggle","memberType":"core.onoff@1"}""")]
    [InlineData("""{"pattern":"enumMap","param":"mode","command":"setMode","memberType":"core.onoff@1"}""")]
    [InlineData("""{"pattern":"threshold","param":"level","then":{"command":"on"},"else":{"command":"off"},"command":"setLevel","memberType":"core.onoff@1"}""")]
    [InlineData("""{"pattern":"threshold","param":"level","above":0,"else":{"command":"off"},"command":"setLevel","memberType":"core.onoff@1"}""")]
    [InlineData("""{"pattern":"threshold","above":0,"then":{"command":"on"},"else":{"command":"off"},"command":"setLevel","memberType":"core.onoff@1"}""")]
    public void Deserialize_SubstitutionWithoutMandatoryMember_ThrowsJsonException(string json)
    {
        // Arrange

        // Act
        var act = () => JsonSerializer.Deserialize<Substitution>(json, ContractsJson.Options);

        // Assert
        act.Should().Throw<JsonException>();
    }

    [Fact]
    public void RoundTrip_NullEntriesInValueCollections_AreKept()
    {
        // Arrange
        var mapping = new Mapping(
            "0/battery/level",
            "0x0001/0x0021",
            [new EnumMapStep(new Dictionary<string, JsonNode?> { ["255"] = null })],
            [null, 255],
            Stateful: new StatefulHandler("multiPress", new Dictionary<string, JsonNode?> { ["window"] = null }));

        // Act
        var json = JsonSerializer.Serialize(mapping, ContractsJson.Options);
        var result = JsonSerializer.Deserialize<Mapping>(json, ContractsJson.Options);

        // Assert
        json.Should().Contain(""""invalidValues":[null,255]"""").And.Contain(""""map":{"255":null}"""")
            .And.Contain(""""config":{"window":null}"""");
        result!.InvalidValues.Should().HaveCount(2).And.Subject.First().Should().BeNull();
        result.Stateful!.Config.Should().ContainKey("window").WhoseValue.Should().BeNull();
    }

    [Fact]
    public void RoundTrip_MemberCallsWithParameters_KeepTheParameters()
    {
        // Arrange
        Substitution substitution = new ThresholdSubstitution(
            "setLevel",
            new TypeRef("core.onoff", 1),
            "level",
            0,
            new MemberCall("on", new Dictionary<string, JsonNode?> { ["transition"] = "PT1S" }),
            new MemberCall("off"));

        // Act
        var json = JsonSerializer.Serialize(substitution, ContractsJson.Options);
        var result = JsonSerializer.Deserialize<Substitution>(json, ContractsJson.Options);

        // Assert
        JsonNode.Parse(json)!["then"]!["parameters"]!["transition"]!.GetValue<string>().Should().Be("PT1S");
        JsonNode.Parse(json)!["else"]!.AsObject().ContainsKey("parameters").Should().BeFalse();
        result.Should().BeOfType<ThresholdSubstitution>()
            .Which.Then.Parameters.Should().ContainKey("transition");
    }

    [Fact]
    public void Serialize_Log10StepWithOneOptionalValue_WritesOnlyThatValue()
    {
        // Arrange
        TransformStep step = new Log10Step(Factor: 10);

        // Act
        var json = JsonSerializer.Serialize(step, ContractsJson.Options);
        var result = JsonSerializer.Deserialize<TransformStep>("""{"fn":"log10","offset":1}""", ContractsJson.Options);

        // Assert
        json.Should().Be("""{"fn":"log10","factor":10}""");
        result.Should().Be(new Log10Step(Offset: 1));
    }
}
