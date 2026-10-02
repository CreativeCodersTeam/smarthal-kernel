using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using AwesomeAssertions;
using SmartHal.Contracts.Addressing;
using SmartHal.Contracts.Api;
using SmartHal.Contracts.Bus;
using SmartHal.Contracts.Primitives;
using SmartHal.Contracts.Runtime;
using SmartHal.Contracts.Serialization;
using Xunit;

namespace SmartHal.Contracts.UnitTests.Api;

/// <summary>
/// Verifies the details of the API contract the round trips do not reach: empty against missing filter criteria,
/// streams of subscription items, nested discriminators out of order and strict reading of history points.
/// </summary>
public sealed class ApiContractTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-09-30T12:00:00Z", CultureInfo.InvariantCulture);

    [Fact]
    public void Deserialize_EmptyObject_GivesAFilterWithoutCriteria()
    {
        // Arrange

        // Act
        var filter = JsonSerializer.Deserialize<Filter>("{}", ContractsJson.Options);

        // Assert
        filter.Should().Be(new Filter());
    }

    [Fact]
    public void RoundTrip_FilterWithEmptyCollections_KeepsThemEmptyInsteadOfNull()
    {
        // Arrange
        // Empty selects nothing, null selects everything: the difference must survive the wire.
        var filter = new Filter(CapabilityTypes: [], Tags: new Dictionary<string, string>(), Kinds: []);

        // Act
        var json = JsonSerializer.Serialize(filter, ContractsJson.Options);
        var result = JsonSerializer.Deserialize<Filter>(json, ContractsJson.Options);

        // Assert
        json.Should().Be("""{"capabilityTypes":[],"tags":{},"kinds":[]}""");
        result!.CapabilityTypes.Should().NotBeNull().And.BeEmpty();
        result.Tags.Should().NotBeNull().And.BeEmpty();
        result.Kinds.Should().NotBeNull().And.BeEmpty();
    }

    [Fact]
    public void RoundTrip_SequenceOfSubscriptionItems_KeepsKindsOrderAndCursors()
    {
        // Arrange
        var address = new Address(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "on");
        SubscriptionItem[] items =
        [
            new SnapshotItem([], "c-0"),
            new MessageItem(new EventOccurred(1, new EventOccurrence(Guid.NewGuid(), address with { Element = "pressed" }, Now, Now)),
                "c-1"),
            new ResyncItem("cursor_expired"),
            new SnapshotItem([new PropertyState(address, true, Now, new Quality(QualityLevel.Good), StateOrigin.Device, 9)], "c-9")
        ];

        // Act
        var json = JsonSerializer.Serialize(items, ContractsJson.Options);
        var result = JsonSerializer.Deserialize<SubscriptionItem[]>(json, ContractsJson.Options)!;

        // Assert
        JsonNode.Parse(json)!.AsArray().Select(item => item!["kind"]!.GetValue<string>())
            .Should().Equal("snapshot", "message", "resync", "snapshot");
        result.Select(item => item.GetType())
            .Should().Equal(typeof(SnapshotItem), typeof(MessageItem), typeof(ResyncItem), typeof(SnapshotItem));
        result[0].Should().BeOfType<SnapshotItem>().Which.States.Should().BeEmpty();
        result[1].Should().BeOfType<MessageItem>().Which.Message.Should().BeOfType<EventOccurred>();
        result[3].Should().BeOfType<SnapshotItem>().Which.Cursor.Should().Be("c-9");
    }

    [Fact]
    public void Deserialize_MessageItemWithBothDiscriminatorsLast_ReadsBothLevels()
    {
        // Arrange
        var original = new MessageItem(
            new CommandUpdated(
                3,
                new CommandInvocation(
                    Guid.NewGuid(), "idem", new Address(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "on"),
                    new Dictionary<string, JsonNode?>(), new Issuer(IssuerKind.User, "u"), CommandStatus.Sent, Now, Now.AddSeconds(5), [])),
            "c-3");
        var node = JsonNode.Parse(JsonSerializer.Serialize<SubscriptionItem>(original, ContractsJson.Options))!.AsObject();
        var reordered = MoveToEnd(node, "kind");
        reordered["message"] = MoveToEnd(reordered["message"]!.AsObject(), "kind");

        // Act
        var result = JsonSerializer.Deserialize<SubscriptionItem>(reordered.ToJsonString(), ContractsJson.Options);

        // Assert
        result.Should().BeOfType<MessageItem>().Which.Message.Should().BeOfType<CommandUpdated>()
            .Which.Invocation.Status.Should().Be(CommandStatus.Sent);
    }

    [Theory]
    [InlineData("Snapshot")]
    [InlineData("MESSAGE")]
    public void Deserialize_SubscriptionItemWithDiscriminatorInOtherCase_ThrowsJsonException(string kind)
    {
        // Arrange
        var json = $$"""{"kind":"{{kind}}","states":[],"cursor":"c"}""";

        // Act
        var act = () => JsonSerializer.Deserialize<SubscriptionItem>(json, ContractsJson.Options);

        // Assert
        act.Should().Throw<JsonException>();
    }

    [Fact]
    public void RoundTrip_InvokeRequestByIds_KeepsAddressAndNullParameters()
    {
        // Arrange
        var address = new Address(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "setColor");
        var request = new InvokeRequest(
            address,
            new Dictionary<string, JsonNode?> { ["color"] = JsonNode.Parse("""{"h":120,"s":50,"v":80}"""), ["transition"] = null },
            new Issuer(IssuerKind.Automation, "abend"),
            "k-1");

        // Act
        var json = JsonSerializer.Serialize(request, ContractsJson.Options);
        var result = JsonSerializer.Deserialize<InvokeRequest>(json, ContractsJson.Options);

        // Assert
        JsonNode.Parse(json)!["address"]!["by"]!.GetValue<string>().Should().Be("id");
        result!.Address.Should().Be(address);
        result.Parameters.Should().ContainKey("transition").WhoseValue.Should().BeNull();
        result.Parameters["color"]!["h"]!.GetValue<int>().Should().Be(120);
    }

    [Fact]
    public void Serialize_HistoryPointWithZeroAggregates_WritesTheZeros()
    {
        // Arrange
        var point = new HistoryPoint(Now, QualityLevel.Good, Min: 0, Max: 0, Avg: 0);

        // Act
        var json = JsonSerializer.Serialize(point, ContractsJson.Options);

        // Assert
        json.Should().Be("""{"ts":"2026-09-30T12:00:00+00:00","quality":"good","min":0,"max":0,"avg":0}""");
    }

    [Theory]
    [InlineData("""{"quality":"good"}""")]
    [InlineData("""{"ts":"2026-09-30T12:00:00Z"}""")]
    public void Deserialize_HistoryPointWithoutMandatoryMember_ThrowsJsonException(string json)
    {
        // Arrange

        // Act
        var act = () => JsonSerializer.Deserialize<HistoryPoint>(json, ContractsJson.Options);

        // Assert
        act.Should().Throw<JsonException>();
    }

    private static JsonObject MoveToEnd(JsonObject node, string property)
    {
        var copy = new JsonObject();

        foreach (var (key, value) in node.Where(pair => pair.Key != property).ToList())
        {
            copy[key] = value?.DeepClone();
        }

        copy[property] = node[property]!.DeepClone();

        return copy;
    }
}
