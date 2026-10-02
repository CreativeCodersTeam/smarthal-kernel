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
/// Verifies the JSON form of the integration contracts: the transform discriminator <c>fn</c>, the substitution
/// discriminator <c>pattern</c> and the mapping example of the formal schema.
/// </summary>
public sealed class IntegrationSerializationTests
{
    public static TheoryData<TransformStep, string> TransformSteps => new()
    {
        { new ScaleStep(0.01), """{"fn":"scale","factor":0.01}""" },
        { new OffsetStep(-40), """{"fn":"offset","value":-40}""" },
        { new InvertStep(100), """{"fn":"invert","max":100}""" },
        { new ReciprocalStep(1_000_000), """{"fn":"reciprocal","k":1000000}""" },
        { new Log10Step(10, 1), """{"fn":"log10","factor":10,"offset":1}""" },
        { new Log10Step(), """{"fn":"log10"}""" },
        {
            new EnumMapStep(new Dictionary<string, JsonNode?> { ["0"] = "closed", ["1"] = "open" }),
            """{"fn":"enumMap","map":{"0":"closed","1":"open"}}"""
        }
    };

    public static TheoryData<Substitution, string> Substitutions => new()
    {
        {
            new ThresholdSubstitution("setLevel", new TypeRef("core.onoff", 1), "level", 0, new MemberCall("on"), new MemberCall("off")),
            """{"pattern":"threshold","param":"level","above":0,"then":{"command":"on"},"else":{"command":"off"},"command":"setLevel","memberType":"core.onoff@1"}"""
        },
        {
            new EnumMapSubstitution(
                "setMode",
                new TypeRef("core.onoff", 1),
                "mode",
                new Dictionary<string, MemberCall> { ["heat"] = new("on"), ["off"] = new("off") }),
            """{"pattern":"enumMap","param":"mode","map":{"heat":{"command":"on"},"off":{"command":"off"}},"command":"setMode","memberType":"core.onoff@1"}"""
        },
        {
            new FixedSubstitution("toggle", new TypeRef("core.onoff", 1), new MemberCall("toggle")),
            """{"pattern":"fixed","use":{"command":"toggle"},"command":"toggle","memberType":"core.onoff@1"}"""
        }
    };

    [Theory]
    [MemberData(nameof(TransformSteps))]
    public void Serialize_TransformStep_WritesFnDiscriminator(TransformStep step, string expectedJson)
    {
        // Arrange

        // Act
        var json = JsonSerializer.Serialize(step, ContractsJson.Options);

        // Assert
        JsonNode.DeepEquals(JsonNode.Parse(json), JsonNode.Parse(expectedJson)).Should().BeTrue(json);
    }

    [Theory]
    [MemberData(nameof(TransformSteps))]
    public void Deserialize_TransformStep_ReadsTheDerivedTypeCompletely(TransformStep expected, string json)
    {
        // Arrange

        // Act
        var step = JsonSerializer.Deserialize<TransformStep>(json, ContractsJson.Options);

        // Assert
        step.Should().BeOfType(expected.GetType());
        var written = JsonSerializer.Serialize(step, ContractsJson.Options);
        JsonNode.DeepEquals(JsonNode.Parse(written), JsonNode.Parse(json)).Should().BeTrue(written);
    }

    [Theory]
    [MemberData(nameof(Substitutions))]
    public void Serialize_Substitution_WritesPatternDiscriminatorAndBaseFields(Substitution substitution, string expectedJson)
    {
        // Arrange

        // Act
        var json = JsonSerializer.Serialize(substitution, ContractsJson.Options);

        // Assert
        JsonNode.DeepEquals(JsonNode.Parse(json), JsonNode.Parse(expectedJson)).Should().BeTrue(json);
    }

    [Theory]
    [MemberData(nameof(Substitutions))]
    public void Deserialize_Substitution_ReadsTheDerivedTypeCompletely(Substitution expected, string json)
    {
        // Arrange

        // Act
        var substitution = JsonSerializer.Deserialize<Substitution>(json, ContractsJson.Options);

        // Assert
        substitution.Should().BeOfType(expected.GetType());
        substitution.Command.Should().Be(expected.Command);
        substitution.MemberType.Should().Be(expected.MemberType);
        var written = JsonSerializer.Serialize(substitution, ContractsJson.Options);
        JsonNode.DeepEquals(JsonNode.Parse(written), JsonNode.Parse(json)).Should().BeTrue(written);
    }

    [Fact]
    public void Serialize_MappingExampleOfTheFormalSchema_ProducesTheDocumentedJson()
    {
        // Arrange
        var mapping = new Mapping(
            "hydraulik/druckseite/value",
            "unit=${slaveId};hr=40002",
            [new ScaleStep(0.01)],
            [JsonValue.Create(65535)],
            TimeSpan.FromSeconds(1));

        // Act
        var json = JsonSerializer.Serialize(mapping, ContractsJson.Options);

        // Assert
        // Verbatim from docs/Formales Schema (C#).md, section "API zur Automationsschicht", example.
        json.Should().Be(
            """{"target":"hydraulik/druckseite/value","address":"unit=${slaveId};hr=40002","transform":[{"fn":"scale","factor":0.01}],"invalidValues":[65535],"pollInterval":"PT1S"}""");
    }

    [Fact]
    public void RoundTrip_InternalBinding_KeepsMembersAggregationsAndSubstitutions()
    {
        // Arrange
        var member = new CapabilityAddress(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        var binding = new Binding(
            Guid.NewGuid(),
            Guid.NewGuid(),
            BindingKind.Internal,
            new Dictionary<string, JsonNode?>(),
            new BindingStatus(BindingState.Online, DateTimeOffset.Parse("2026-09-30T12:00:00Z",
                System.Globalization.CultureInfo.InvariantCulture)),
            Internal: new InternalBinding(
                [member],
                new Dictionary<string, Aggregation> { ["on"] = Aggregation.All },
                [new FixedSubstitution("toggle", new TypeRef("core.onoff", 1), new MemberCall("toggle"))]));

        // Act
        var json = JsonSerializer.Serialize(binding, ContractsJson.Options);
        var result = JsonSerializer.Deserialize<Binding>(json, ContractsJson.Options);

        // Assert
        var node = JsonNode.Parse(json)!;
        node["kind"]!.GetValue<string>().Should().Be("internal");
        node["status"]!["state"]!.GetValue<string>().Should().Be("online");
        node["internal"]!["aggregations"]!["on"]!.GetValue<string>().Should().Be("all");
        node.AsObject().ContainsKey("adapterId").Should().BeFalse("null properties are not written");
        result!.Internal!.Members.Should().Equal(member);
        result.Internal.Substitutions.Should().ContainSingle().Which.Should().BeOfType<FixedSubstitution>();
        JsonNode.DeepEquals(JsonNode.Parse(JsonSerializer.Serialize(result, ContractsJson.Options)), node).Should().BeTrue();
    }

    [Fact]
    public void Serialize_Protocols_AreLowerCaseNames()
    {
        // Arrange
        string[] protocols = [Protocols.Matter, Protocols.Zigbee, Protocols.Modbus, Protocols.OpcUa, Protocols.Mqtt];

        // Act

        // Assert
        protocols.Should().Equal("matter", "zigbee", "modbus", "opcua", "mqtt");
    }
}
