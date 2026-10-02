using System.Text.Json;
using System.Text.Json.Nodes;
using AwesomeAssertions;
using SmartHal.Contracts.Primitives;
using SmartHal.Contracts.Schema;
using SmartHal.Contracts.Serialization;
using SmartHal.Core.Catalog;
using Xunit;

namespace SmartHal.Core.UnitTests.Catalog;

/// <summary>
/// Verifies the shape of the core catalog: 25 capability types in five groups, unique names, version 1.0, a lossless
/// JSON round trip for every entry, and the type catalog built from it.
/// </summary>
public sealed class CoreCapabilityCatalogTests
{
    public static TheoryData<string> CapabilityNames => [.. CoreCapabilityCatalog.All.Select(capability => capability.Name)];

    [Fact]
    public void All_Always_ListsTheTwentyFiveCapabilitiesInCatalogOrder()
    {
        // Arrange

        // Act
        var names = CoreCapabilityCatalog.All.Select(capability => capability.Name);

        // Assert
        names.Should().Equal(
            "core.deviceinfo", "core.connectivity", "core.identify", "core.firmware", "core.battery", "core.bridge", "core.alarms",
            "core.onoff", "core.level", "core.color", "core.cover", "core.thermostat", "core.lock",
            "core.temperature", "core.humidity", "core.contact", "core.motion", "core.button",
            "core.power",
            "core.pressure", "core.flow", "core.rotationalspeed", "core.filllevel", "core.vibration", "core.runstate");
    }

    [Fact]
    public void Groups_Always_HaveTheCountsOfTheCatalogTable()
    {
        // Arrange

        // Act
        int[] counts =
        [
            SystemCapabilities.All.Count,
            ActuatorCapabilities.All.Count,
            SensorCapabilities.All.Count,
            EnergyCapabilities.All.Count,
            IiotCapabilities.All.Count
        ];

        // Assert
        counts.Should().Equal(7, 6, 5, 1, 6);
    }

    [Fact]
    public void All_Always_HasUniqueNamesInTheCoreNamespaceAndVersionOneDotZero()
    {
        // Arrange

        // Act
        var all = CoreCapabilityCatalog.All;

        // Assert
        all.Select(capability => capability.Name).Should().OnlyHaveUniqueItems();
        all.Should().AllSatisfy(capability =>
        {
            capability.Name.Should().StartWith("core.");
            capability.Version.Should().Be(new TypeVersion(1, 0));
        });
    }

    [Theory]
    [MemberData(nameof(CapabilityNames))]
    public void RoundTrip_CatalogEntry_ProducesIdenticalJson(string name)
    {
        // Arrange
        var capability = CoreCapabilityCatalog.All.Single(entry => entry.Name == name);
        var json = JsonSerializer.Serialize(capability, ContractsJson.Options);

        // Act
        var read = JsonSerializer.Deserialize<CapabilityType>(json, ContractsJson.Options);
        var written = JsonSerializer.Serialize(read, ContractsJson.Options);

        // Assert
        JsonNode.DeepEquals(JsonNode.Parse(written), JsonNode.Parse(json)).Should().BeTrue(written);
    }

    [Fact]
    public void RoundTrip_HsvDataType_ProducesIdenticalJson()
    {
        // Arrange
        var json = JsonSerializer.Serialize(CoreDataTypes.Hsv, ContractsJson.Options);

        // Act
        var written = JsonSerializer.Serialize(
            JsonSerializer.Deserialize<Contracts.DataTypes.DataTypeDef>(json, ContractsJson.Options),
            ContractsJson.Options);

        // Assert
        JsonNode.DeepEquals(JsonNode.Parse(written), JsonNode.Parse(json)).Should().BeTrue(written);
    }

    [Fact]
    public void ToTypeCatalog_Always_HoldsTheCapabilitiesAndTheHsvDataTypeOnly()
    {
        // Arrange

        // Act
        var catalog = CoreCapabilityCatalog.ToTypeCatalog();

        // Assert
        // Every access builds a new graph, and records compare their collections by reference, so the entries are
        // compared through their JSON form.
        JsonOf(catalog.Capabilities).Should().Be(JsonOf(CoreCapabilityCatalog.All));
        JsonOf(catalog.DataTypes).Should().Be(JsonOf(new[] { CoreDataTypes.Hsv }));
        catalog.Profiles.Should().BeEmpty();
        catalog.DeviceTypes.Should().BeEmpty();
        catalog.Migrations.Should().BeNull();
    }

    [Fact]
    public void All_Always_IsReadOnly()
    {
        // Arrange
        var all = CoreCapabilityCatalog.All;

        // Act
        var listIsReadOnly = all is not IList<CapabilityType> list || list.IsReadOnly;
        var mapIsReadOnly = all[0].Properties is not IDictionary<string, PropertyDef> map || map.IsReadOnly;

        // Assert
        listIsReadOnly.Should().BeTrue("the catalog must not be changed by a caller");
        mapIsReadOnly.Should().BeTrue("the maps of a definition must not be changed by a caller");
    }

    private static string JsonOf<T>(T value) => JsonSerializer.Serialize(value, ContractsJson.Options);
}
