using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using AwesomeAssertions;
using SmartHal.Contracts.Addressing;
using SmartHal.Contracts.Api;
using SmartHal.Contracts.Bus;
using SmartHal.Contracts.DataTypes;
using SmartHal.Contracts.Primitives;
using SmartHal.Contracts.Runtime;
using SmartHal.Contracts.Schema;
using SmartHal.Contracts.Serialization;
using Xunit;

namespace SmartHal.Contracts.UnitTests.Api;

/// <summary>
/// Verifies the JSON form of the API models: the subscription discriminator <c>kind</c>, the history point with its
/// rollup aggregates, the invoke request with both address forms and the type catalog with reusable data types and
/// migrations.
/// </summary>
public sealed class ApiSerializationTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-09-30T12:00:00Z", CultureInfo.InvariantCulture);

    private static readonly Address Level = new Address(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "level");

    public static TheoryData<SubscriptionItem, string> SubscriptionItems =>
        new TheoryData<SubscriptionItem, string>
    {
        {
            new SnapshotItem([new PropertyState(Level, 40, Now, new Quality(QualityLevel.Good), StateOrigin.Virtual, 1)], "c-1"),
            "snapshot"
        },
        {
            new MessageItem(
                new StateChanged(2, new PropertyState(Level, 50, Now, new Quality(QualityLevel.Good), StateOrigin.Virtual, 2), 40),
                "c-2"),
            "message"
        },
        { new ResyncItem("cursor_expired"), "resync" }
    };

    [Theory]
    [MemberData(nameof(SubscriptionItems))]
    public void RoundTrip_SubscriptionItem_KeepsKindAndContent(SubscriptionItem item, string expectedKind)
    {
        // Arrange

        // Act
        var json = JsonSerializer.Serialize(item, ContractsJson.Options);
        var result = JsonSerializer.Deserialize<SubscriptionItem>(json, ContractsJson.Options);

        // Assert
        var node = JsonNode.Parse(json)!;
        node["kind"]!.GetValue<string>().Should().Be(expectedKind);
        result.Should().BeOfType(item.GetType());
        JsonNode.DeepEquals(JsonNode.Parse(JsonSerializer.Serialize(result, ContractsJson.Options)), node).Should().BeTrue(json);
    }

    [Fact]
    public void Serialize_MessageItem_NestsTheBusMessageWithItsOwnKind()
    {
        // Arrange
        SubscriptionItem item = new MessageItem(
            new StateChanged(2, new PropertyState(Level, 50, Now, new Quality(QualityLevel.Good), StateOrigin.Virtual, 2)),
            "c-2");

        // Act
        var node = JsonNode.Parse(JsonSerializer.Serialize(item, ContractsJson.Options))!;

        // Assert
        node["kind"]!.GetValue<string>().Should().Be("message");
        node["message"]!["kind"]!.GetValue<string>().Should().Be("StateChanged");
        node["cursor"]!.GetValue<string>().Should().Be("c-2");
    }

    [Fact]
    public void RoundTrip_HistoryPoints_KeepRawValueAndRollupAggregatesIncludingLast()
    {
        // Arrange
        HistoryPoint[] points =
        [
            new HistoryPoint(Now, QualityLevel.Good, Value: 8.2),
            new HistoryPoint(Now, QualityLevel.Uncertain, Min: 7.9, Max: 8.6, Avg: 8.2, Last: 8.4),
            new HistoryPoint(Now, QualityLevel.Good, Last: "running")
        ];

        // Act
        var json = JsonSerializer.Serialize(points, ContractsJson.Options);
        var result = JsonSerializer.Deserialize<HistoryPoint[]>(json, ContractsJson.Options);

        // Assert
        var node = JsonNode.Parse(json)!.AsArray();
        node[0]!.AsObject().Select(pair => pair.Key).Should().Equal("ts", "quality", "value");
        node[1]!["quality"]!.GetValue<string>().Should().Be("uncertain");
        node[1]!["last"]!.GetValue<double>().Should().Be(8.4);
        node[2]!["last"]!.GetValue<string>().Should().Be("running");
        result![1].Avg.Should().Be(8.2);
        result[2].Last!.GetValue<string>().Should().Be("running");
    }

    [Fact]
    public void RoundTrip_InvokeRequestByKeys_KeepsAddressFormParametersAndIssuer()
    {
        // Arrange
        var request = new InvokeRequest(
            new KeyAddress("flur.alle-lichter", "1", "level", "setLevel"),
            new Dictionary<string, JsonNode?> { ["level"] = 50, ["transition"] = "PT1S" },
            new Issuer(IssuerKind.User, "chris"),
            "3f1c9a");

        // Act
        var json = JsonSerializer.Serialize(request, ContractsJson.Options);
        var result = JsonSerializer.Deserialize<InvokeRequest>(json, ContractsJson.Options);

        // Assert
        var node = JsonNode.Parse(json)!;
        node["address"]!["by"]!.GetValue<string>().Should().Be("key");
        node["issuer"]!["kind"]!.GetValue<string>().Should().Be("user");
        node["idempotencyKey"]!.GetValue<string>().Should().Be("3f1c9a");
        result!.Address.Should().Be(request.Address);
        result.Parameters["level"]!.GetValue<int>().Should().Be(50);
    }

    [Fact]
    public void Deserialize_InvokeRequestWithoutIdempotencyKey_ThrowsJsonException()
    {
        // Arrange
        const string json =
            """{"address":{"by":"key","device":"a","channel":"b","capability":"c","element":"d"},"parameters":{},"issuer":{"kind":"user","id":"u"}}""";

        // Act
        var act = () => JsonSerializer.Deserialize<InvokeRequest>(json, ContractsJson.Options);

        // Assert
        act.Should().Throw<JsonException>("the idempotency key is mandatory");
    }

    [Fact]
    public void Serialize_EmptyFilter_WritesAnEmptyObject()
    {
        // Arrange
        var filter = new Filter();

        // Act
        var json = JsonSerializer.Serialize(filter, ContractsJson.Options);

        // Assert
        json.Should().Be("{}");
    }

    [Fact]
    public void RoundTrip_Filter_KeepsEveryCriterion()
    {
        // Arrange
        var filter = new Filter(
            Guid.NewGuid(),
            "halle2.*",
            [new TypeRef("core.pressure", 1)],
            new Dictionary<string, string> { ["gewerk"] = "hlk" },
            ["StateChanged"]);

        // Act
        var json = JsonSerializer.Serialize(filter, ContractsJson.Options);
        var result = JsonSerializer.Deserialize<Filter>(json, ContractsJson.Options);

        // Assert
        JsonNode.Parse(json)!["capabilityTypes"]![0]!.GetValue<string>().Should().Be("core.pressure@1");
        result!.CapabilityTypes.Should().Equal(new TypeRef("core.pressure", 1));
        result.Kinds.Should().Equal("StateChanged");
        result.Tags.Should().Contain("gewerk", "hlk");
    }

    [Fact]
    public void RoundTrip_TypeCatalog_KeepsDataTypesAndMigrations()
    {
        // Arrange
        var catalog = new TypeCatalog(
            [],
            [],
            [],
            [new DataTypeDef("core.types.hsv", new TypeVersion(1, 0), new ObjectType(new Dictionary<string,
                DataType> { ["h"] = new NumberType("deg") }))],
            [new CapabilityMigration(new TypeRef("core.level", 1), new TypeRef("core.level", 2))]);

        // Act
        var json = JsonSerializer.Serialize(catalog, ContractsJson.Options);
        var result = JsonSerializer.Deserialize<TypeCatalog>(json, ContractsJson.Options);

        // Assert
        json.Should().Be(
            """{"capabilities":[],"profiles":[],"deviceTypes":[],"dataTypes":[{"name":"core.types.hsv","version":"1.0","dataType":{"type":"object","fields":{"h":{"type":"number","unit":"deg"}}}}],"migrations":[{"from":"core.level@1","to":"core.level@2"}]}""");
        result!.DataTypes.Should().ContainSingle().Which.Name.Should().Be("core.types.hsv");
        result.Migrations.Should().ContainSingle().Which.To.Should().Be(new TypeRef("core.level", 2));
    }

    [Fact]
    public void Deserialize_TypeCatalogWithoutOptionalLists_LeavesThemNull()
    {
        // Arrange
        const string json = """{"capabilities":[],"profiles":[],"deviceTypes":[]}""";

        // Act
        var catalog = JsonSerializer.Deserialize<TypeCatalog>(json, ContractsJson.Options);

        // Assert
        catalog!.DataTypes.Should().BeNull();
        catalog.Migrations.Should().BeNull();
    }
}
