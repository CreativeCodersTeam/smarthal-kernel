using System.Collections;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using AwesomeAssertions;
using SmartHal.Contracts.Schema;
using SmartHal.Contracts.Serialization;
using SmartHal.Core.Catalog;
using Xunit;

namespace SmartHal.Core.UnitTests.Catalog;

/// <summary>
/// Verifies that the catalog hands out no shared mutable state: every access builds a new object graph, JSON values
/// belong to one graph only, and every collection in a graph is a read-only wrapper at every depth.
/// </summary>
public sealed class CatalogIsolationTests
{
    public static TheoryData<string> CapabilityNames => [.. CoreCapabilityCatalog.All.Select(capability => capability.Name)];

    [Theory]
    [MemberData(nameof(CapabilityNames))]
    public void All_AccessedTwice_ReturnsDifferentInstancesWithIdenticalJson(string name)
    {
        // Arrange

        // Act
        var first = CoreCapabilityCatalog.All.Single(capability => capability.Name == name);
        var second = CoreCapabilityCatalog.All.Single(capability => capability.Name == name);

        // Assert
        second.Should().NotBeSameAs(first);
        second.Properties.Should().NotBeSameAs(first.Properties);
        JsonOf(second).Should().Be(JsonOf(first));
    }

    // Every catalog accessor that has to build a new instance per access, keyed by a readable name.
    private static readonly IReadOnlyDictionary<string, Func<object>> Accessors = new Dictionary<string, Func<object>>
    {
        [nameof(SystemCapabilities) + "." + nameof(SystemCapabilities.Battery)] = () => SystemCapabilities.Battery,
        [nameof(ActuatorCapabilities) + "." + nameof(ActuatorCapabilities.Color)] = () => ActuatorCapabilities.Color,
        [nameof(SensorCapabilities) + "." + nameof(SensorCapabilities.Temperature)] = () => SensorCapabilities.Temperature,
        [nameof(EnergyCapabilities) + "." + nameof(EnergyCapabilities.Power)] = () => EnergyCapabilities.Power,
        [nameof(IiotCapabilities) + "." + nameof(IiotCapabilities.Pressure)] = () => IiotCapabilities.Pressure,
        [nameof(SystemCapabilities) + "." + nameof(SystemCapabilities.All)] = () => SystemCapabilities.All,
        [nameof(CoreDataTypes) + "." + nameof(CoreDataTypes.Hsv)] = () => CoreDataTypes.Hsv,
        [nameof(CoreCapabilityCatalog) + "." + nameof(CoreCapabilityCatalog.ToTypeCatalog)] = CoreCapabilityCatalog.ToTypeCatalog
    };

    public static TheoryData<string> AccessorNames => [.. Accessors.Keys];

    [Theory]
    [MemberData(nameof(AccessorNames))]
    public void Accessor_CalledTwice_ReturnsDifferentInstances(string accessor)
    {
        // Arrange
        var access = Accessors[accessor];

        // Act
        var first = access();
        var second = access();

        // Assert
        second.Should().NotBeSameAs(first, $"{accessor} must build a new instance on every access");
    }

    [Fact]
    public void AlarmParameterDefault_AttachedFromTwoAccesses_DoesNotThrow()
    {
        // Arrange
        var firstDefault = IiotCapabilities.Pressure.Alarms["highLimit"].Parameters!["delay"].Default;
        var secondDefault = IiotCapabilities.Pressure.Alarms["highLimit"].Parameters!["delay"].Default;
        var firstOwner = new JsonObject();
        var secondOwner = new JsonObject();

        // Act
        // A JsonNode can have one parent only; a default shared between accesses would throw on the second attach.
        var act = () =>
        {
            firstOwner["delay"] = firstDefault;
            secondOwner["delay"] = secondDefault;
        };

        // Assert
        act.Should().NotThrow();
        secondDefault.Should().NotBeSameAs(firstDefault);
        secondOwner["delay"]!.GetValue<string>().Should().Be("PT10S");
    }

    [Theory]
    [MemberData(nameof(CapabilityNames))]
    public void CatalogEntry_WalkedCompletely_HoldsOnlyReadOnlyCollectionWrappers(string name)
    {
        // Arrange
        var capability = CoreCapabilityCatalog.All.Single(entry => entry.Name == name);

        // Act
        var collections = CollectionsOf(capability, name).ToList();

        // Assert
        collections.Should().NotBeEmpty();
        collections.Should().AllSatisfy(collection =>
        {
            IsMutableCollectionType(collection.Value.GetType()).Should().BeFalse(
                $"{collection.Path} is a {collection.Value.GetType()}, which a caller could change by casting");
            IsReadOnly(collection.Value).Should().BeTrue($"{collection.Path} must reject changes");
        });
    }

    [Fact]
    public void TypeCatalogAndHsv_WalkedCompletely_HoldOnlyReadOnlyCollectionWrappers()
    {
        // Arrange
        var catalog = CoreCapabilityCatalog.ToTypeCatalog();

        // Act
        var collections = CollectionsOf(catalog, "catalog").Concat(CollectionsOf(CoreDataTypes.Hsv, "hsv")).ToList();

        // Assert
        collections.Should().AllSatisfy(collection =>
        {
            IsMutableCollectionType(collection.Value.GetType()).Should().BeFalse(
                $"{collection.Path} is a {collection.Value.GetType()}, which a caller could change by casting");
            IsReadOnly(collection.Value).Should().BeTrue($"{collection.Path} must reject changes");
        });
    }

    [Fact]
    public void Collections_CastToMutableTypes_CannotBeChanged()
    {
        // Arrange
        var all = CoreCapabilityCatalog.All;
        var aggregates = SensorCapabilities.Temperature.Properties["value"].History!.Rollups![0].Aggregates;
        var properties = SensorCapabilities.Temperature.Properties;

        // Act
        var addToAll = () => ((ICollection<CapabilityType>)all).Add(SensorCapabilities.Contact);
        var overwriteAggregate = () => ((IList<RollupAggregate>)aggregates)[0] = RollupAggregate.Max;
        var removeProperty = () => ((IDictionary<string, PropertyDef>)properties).Remove("value");

        // Assert
        (aggregates as RollupAggregate[]).Should().BeNull();
        (all as List<CapabilityType>).Should().BeNull();
        (properties as Dictionary<string, PropertyDef>).Should().BeNull();
        addToAll.Should().Throw<NotSupportedException>();
        overwriteAggregate.Should().Throw<NotSupportedException>();
        removeProperty.Should().Throw<NotSupportedException>();
    }

    private static string JsonOf<T>(T value) => JsonSerializer.Serialize(value, ContractsJson.Options);

    private static bool IsMutableCollectionType(Type type) =>
        type.IsArray
        || (type.IsGenericType
            && (type.GetGenericTypeDefinition() == typeof(List<>) || type.GetGenericTypeDefinition() == typeof(Dictionary<,>)));

    private static bool IsReadOnly(object collection)
    {
        if (collection is IList list)
        {
            return list.IsReadOnly;
        }

        if (collection is IDictionary dictionary)
        {
            return dictionary.IsReadOnly;
        }

        // Neither non-generic interface is implemented: read-only is decided by the generic ICollection<T>, if any.
        var genericCollection = collection.GetType()
            .GetInterfaces()
            .FirstOrDefault(type => type.IsGenericType && type.GetGenericTypeDefinition() == typeof(ICollection<>));

        return genericCollection is null
            || (bool)genericCollection.GetProperty(nameof(ICollection<object>.IsReadOnly))!.GetValue(collection)!;
    }

    private static IEnumerable<(string Path, object Value)> CollectionsOf(object? node, string path)
    {
        if (node is null or string or JsonNode || node.GetType().IsValueType)
        {
            yield break;
        }

        if (node is IEnumerable enumerable)
        {
            yield return (path, node);

            var index = 0;

            foreach (var item in enumerable)
            {
                var (itemPath, itemValue) = Unwrap(item, $"{path}[{index++}]");

                foreach (var nested in CollectionsOf(itemValue, itemPath))
                {
                    yield return nested;
                }
            }

            yield break;
        }

        foreach (var property in node.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (property.GetIndexParameters().Length > 0)
            {
                continue;
            }

            foreach (var nested in CollectionsOf(property.GetValue(node), $"{path}.{property.Name}"))
            {
                yield return nested;
            }
        }
    }

    private static (string Path, object? Value) Unwrap(object? item, string path)
    {
        if (item is null)
        {
            return (path, null);
        }

        var type = item.GetType();

        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(KeyValuePair<,>))
        {
            var key = type.GetProperty("Key")!.GetValue(item);

            return ($"{path}:{key}", type.GetProperty("Value")!.GetValue(item));
        }

        return (path, item);
    }
}
