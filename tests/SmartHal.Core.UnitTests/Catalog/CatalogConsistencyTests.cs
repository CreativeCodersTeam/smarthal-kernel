using AwesomeAssertions;
using SmartHal.Contracts.DataTypes;
using SmartHal.Contracts.Schema;
using SmartHal.Core.Catalog;
using Xunit;

namespace SmartHal.Core.UnitTests.Catalog;

/// <summary>
/// Verifies that every definition of the core catalog is consistent in itself: commands name existing properties
/// and parameters, alarms name existing events and properties, features are declared, and reusable types resolve.
/// </summary>
public sealed class CatalogConsistencyTests
{
    public static TheoryData<string> CapabilityNames => [.. CoreCapabilityCatalog.All.Select(capability => capability.Name)];

    [Theory]
    [MemberData(nameof(CapabilityNames))]
    public void Commands_OfEveryCapability_NameExistingPropertiesAndParameters(string name)
    {
        // Arrange
        var capability = Find(name);

        // Act
        var commands = capability.Commands.Values;

        // Assert
        commands.Should().AllSatisfy(command =>
        {
            (command.Affects ?? []).Should().BeSubsetOf(capability.Properties.Keys);
            (command.RequiredParameters ?? []).Should().BeSubsetOf(command.Parameters?.Keys ?? []);
        });
    }

    [Theory]
    [MemberData(nameof(CapabilityNames))]
    public void Commands_OfEveryCapability_MatchTheirCompletionMode(string name)
    {
        // Arrange
        var capability = Find(name);

        // Act
        var commands = capability.Commands.Values;

        // Assert
        commands.Where(command => command.Completion == Completion.Result)
            .Should().AllSatisfy(command => command.Result.Should().NotBeNull());
        commands.Where(command => command.Completion == Completion.Confirmed)
            .Should().AllSatisfy(command => command.Affects.Should().NotBeNullOrEmpty());
        commands.Should().AllSatisfy(command => command.Timeout.Should().BePositive());
    }

    [Theory]
    [MemberData(nameof(CapabilityNames))]
    public void Alarms_OfEveryCapability_NameExistingEventsAndProperties(string name)
    {
        // Arrange
        var capability = Find(name);

        // Act
        var sources = capability.Alarms.Values.Select(alarm => alarm.Source);

        // Assert
        sources.Should().AllSatisfy(source =>
        {
            switch (source)
            {
                case DeviceAlarmSource device:
                    capability.Events.Keys.Should().Contain(device.Event);
                    break;
                case RuleAlarmSource rule:
                    capability.Properties.Keys.Should().Contain(rule.Property);
                    break;
                default:
                    throw new InvalidOperationException($"Unexpected alarm source {source.GetType().Name}.");
            }
        });
    }

    [Theory]
    [MemberData(nameof(CapabilityNames))]
    public void Features_UsedByPropertiesAndCommands_AreDeclared(string name)
    {
        // Arrange
        var capability = Find(name);

        // Act
        var used = capability.Properties.Values.Select(property => property.Feature)
            .Concat(capability.Commands.Values.Select(command => command.Feature))
            .OfType<string>()
            .Distinct();

        // Assert
        used.Should().BeSubsetOf(capability.Features ?? []);
    }

    [Theory]
    [MemberData(nameof(CapabilityNames))]
    public void Properties_OfEveryCapability_FollowTheHistoryConventions(string name)
    {
        // Arrange
        var capability = Find(name);

        // Act
        var properties = capability.Properties.Values;

        // Assert
        // Strings and timestamps are not historized; Booleans and enums keep raw values without compaction.
        properties.Where(property => property.DataType is StringType or TimestampType)
            .Should().AllSatisfy(property => property.History.Should().BeNull());
        properties.Where(property => property.DataType is BooleanType or EnumType)
            .Should().AllSatisfy(property =>
            {
                property.History.Should().NotBeNull();
                property.History.Rollups.Should().BeNull();
            });
        properties.Where(property => property.DataType is NumericType)
            .Should().AllSatisfy(property =>
            {
                property.History.Should().NotBeNull();
                property.History.Rollups.Should().NotBeNullOrEmpty();
            });
    }

    [Fact]
    public void RefTypes_InTheCatalog_ResolveToTheHsvDataType()
    {
        // Arrange
        var dataTypes = CoreCapabilityCatalog.All
            .SelectMany(capability => capability.Properties.Values.Select(property => property.DataType)
                .Concat(capability.Commands.Values.SelectMany(command => command.Parameters?.Values ?? [])));

        // Act
        var references = dataTypes.OfType<RefType>().Select(reference => reference.Ref).Distinct();

        // Assert
        references.Should().Equal(CoreDataTypes.HsvRef);
        CoreDataTypes.HsvRef.Name.Should().Be(CoreDataTypes.Hsv.Name);
        CoreDataTypes.HsvRef.Major.Should().Be(CoreDataTypes.Hsv.Version.Major);
    }

    [Fact]
    public void StructsInTheCatalog_AreAtMostTwoLevelsDeep()
    {
        // Arrange
        var dataTypes = CoreCapabilityCatalog.All
            .SelectMany(capability => capability.Properties.Values.Select(property => property.DataType)
                .Concat(capability.Commands.Values.SelectMany(command => (command.Parameters?.Values ?? []).Append(command.Result)))
                .Concat(capability.Events.Values.Select(eventDef => eventDef.Payload)))
            .OfType<DataType>()
            .Append(CoreDataTypes.Hsv.DataType);

        // Act
        var deepest = dataTypes.Max(Depth);

        // Assert
        deepest.Should().BeLessThanOrEqualTo(2);
    }

    private static CapabilityType Find(string name) => CoreCapabilityCatalog.All.Single(capability => capability.Name == name);

    private static int Depth(DataType dataType) => dataType switch
    {
        ObjectType struct_ => 1 + struct_.Fields.Values.Select(Depth).DefaultIfEmpty(0).Max(),
        ArrayType array => Depth(array.Items),
        _ => 0
    };
}
