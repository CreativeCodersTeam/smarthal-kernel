using System.Text.Json.Nodes;
using AwesomeAssertions;
using SmartHal.Contracts.DataTypes;
using SmartHal.Contracts.Primitives;
using SmartHal.Contracts.Schema;
using SmartHal.Contracts.Topology;
using SmartHal.Core.Abstractions.Validation;
using SmartHal.Core.Validation;
using Xunit;
using static SmartHal.Core.UnitTests.Validation.ValidationFixtures;

namespace SmartHal.Core.UnitTests.Validation;

/// <summary>
/// Verifies that a missing value is reported exactly once as <see cref="ValidationCodes.NullEntry"/> at its own path
/// and never cascades into a second, misleading code; a key whose value is <see langword="null"/> still counts as
/// declared.
/// </summary>
public sealed class NullHandlingTests
{
    private readonly ContractValidator _sut = new ContractValidator();

    [Fact]
    public void Validate_RequiredParameterWhoseTypeIsNull_ReportsOnlyTheNullEntry()
    {
        // Arrange
        var level = Level();
        var setLevel = level.Commands["setLevel"] with
        {
            Parameters = new Dictionary<string, DataType> { ["level"] = null!, ["transition"] = new DurationType() }
        };
        var type = level with { Commands = new Dictionary<string, CommandDef>(level.Commands) { ["setLevel"] = setLevel } };

        // Act
        var errors = _sut.Validate(type);

        // Assert
        errors.CodesAndPaths().Should().Equal((ValidationCodes.NullEntry, "commands.setLevel.parameters.level"));
    }

    [Fact]
    public void Validate_RequiredFieldWhoseTypeIsNull_ReportsOnlyTheNullEntry()
    {
        // Arrange
        var hsv = Hsv() with
        {
            DataType = new ObjectType(
                new Dictionary<string, DataType> { ["h"] = null!, ["s"] = new NumberType(), ["v"] = new NumberType() },
                ["h", "s", "v"])
        };

        // Act
        var errors = _sut.Validate(hsv);

        // Assert
        errors.CodesAndPaths().Should().Equal((ValidationCodes.NullEntry, "dataType.fields.h"));
    }

    [Fact]
    public void Validate_StructWithoutFields_ReportsOnlyTheMissingFields()
    {
        // Arrange
        var hsv = Hsv() with { DataType = new ObjectType(null!, ["h"]) };

        // Act
        var errors = _sut.Validate(hsv);

        // Assert
        errors.CodesAndPaths().Should().Equal((ValidationCodes.NullEntry, "dataType.fields"));
    }

    [Fact]
    public void Validate_EnumWithoutValues_ReportsOnlyTheMissingValues()
    {
        // Arrange
        var level = Level();
        var type = level with
        {
            Properties = new Dictionary<string, PropertyDef>(level.Properties) { ["mode"] = new PropertyDef(new EnumType(null!),
                PropertyCategory.State) }
        };

        // Act
        var errors = _sut.Validate(type);

        // Assert
        errors.CodesAndPaths().Should().Equal((ValidationCodes.NullEntry, "properties.mode.dataType.values"));
    }

    [Theory]
    [InlineData("properties")]
    [InlineData("commands")]
    [InlineData("events")]
    [InlineData("alarms")]
    public void Validate_CapabilityTypeWithoutMandatoryMap_ReportsOnlyTheMissingMap(string map)
    {
        // Arrange
        var level = Level();
        var type = map switch
        {
            "properties" => level with { Properties = null! },
            "commands" => level with { Commands = null! },
            "events" => level with { Events = null! },
            _ => level with { Alarms = null! }
        };

        // Act
        var errors = _sut.Validate(type);

        // Assert
        // affects, the rule alarm on "level" and the device alarm on "blocked" refer into the missing maps; they
        // are not judged, so no unknown_property or unknown_event follows.
        errors.CodesAndPaths().Should().Equal((ValidationCodes.NullEntry, map));
    }

    [Fact]
    public void Validate_AlarmSourcesWithoutName_ReportNullEntryAtTheName()
    {
        // Arrange
        var level = Level();
        var type = level with
        {
            Alarms = new Dictionary<string, AlarmDef>
            {
                ["highLimit"] = level.Alarms["highLimit"] with { Source = new RuleAlarmSource(null!, AlarmCondition.Above) },
                ["blockedAlarm"] = level.Alarms["blockedAlarm"] with { Source = new DeviceAlarmSource(null!) }
            }
        };

        // Act
        var errors = _sut.Validate(type);

        // Assert
        errors.CodesAndPaths().Should().Equal(
            (ValidationCodes.NullEntry, "alarms.highLimit.source.property"),
            (ValidationCodes.NullEntry, "alarms.blockedAlarm.source.event"));
    }

    [Fact]
    public void Validate_ConfirmedCommandWithNullAffectsEntry_ReportsOnlyTheNullEntry()
    {
        // Arrange
        var level = Level();
        var setLevel = level.Commands["setLevel"] with { Affects = [null!] };
        var type = level with { Commands = new Dictionary<string, CommandDef>(level.Commands) { ["setLevel"] = setLevel } };

        // Act
        var errors = _sut.Validate(type);

        // Assert
        errors.CodesAndPaths().Should().Equal((ValidationCodes.NullEntry, "commands.setLevel.affects[0]"));
    }

    [Fact]
    public void Validate_ProfileWithoutCapabilities_ReportsOnlyTheMissingList()
    {
        // Arrange
        var profile = DimmerProfile() with { Capabilities = null! };

        // Act
        var errors = _sut.Validate(profile);

        // Assert
        errors.CodesAndPaths().Should().Equal((ValidationCodes.NullEntry, "capabilities"));
    }

    [Fact]
    public void Validate_DeviceTypeWithoutChannels_ReportsNoRootChannelViolation()
    {
        // Arrange
        var deviceType = Dimmer() with { Channels = null! };

        // Act
        var errors = _sut.Validate(deviceType);

        // Assert
        errors.CodesAndPaths().Should().Equal((ValidationCodes.NullEntry, "channels"));
    }

    [Theory]
    [InlineData("parameters")]
    [InlineData("mappings")]
    public void Validate_BindingTemplateWithoutMandatoryCollection_ReportsTheMissingCollection(string member)
    {
        // Arrange
        var deviceType = Dimmer();
        var template = member == "parameters"
            ? deviceType.BindingTemplates![0] with { Parameters = null! }
            : deviceType.BindingTemplates![0] with { Mappings = null! };

        // Act
        var errors = _sut.Validate(deviceType with { BindingTemplates = [template] });

        // Assert
        errors.CodesAndPaths().Should().Equal((ValidationCodes.NullEntry, $"bindingTemplates[0].{member}"));
    }

    [Fact]
    public void Validate_CatalogChannelTemplateWithoutCapabilities_ReportsNoProfileViolation()
    {
        // Arrange
        var catalog = TestCatalog();
        var dimmer = Dimmer();
        catalog = catalog with
        {
            DeviceTypes = [dimmer with { Channels = [dimmer.Channels[0], dimmer.Channels[1] with { Capabilities = null! }] }]
        };

        // Act
        var errors = _sut.Validate(catalog);

        // Assert
        errors.CodesAndPaths().Should().Equal((ValidationCodes.NullEntry, "deviceTypes[0].channels[1].capabilities"));
    }

    [Fact]
    public void Validate_CatalogWithoutMandatoryLists_ReportsOnlyTheMissingLists()
    {
        // Arrange
        // The device type and the migration refer into the missing lists; those references are not judged again.
        var catalog = TestCatalog() with { Capabilities = null!, Profiles = null! };

        // Act
        var errors = _sut.Validate(catalog);

        // Assert
        errors.CodesAndPaths().Should().Equal(
            (ValidationCodes.NullEntry, "capabilities"),
            (ValidationCodes.NullEntry, "profiles"));
    }

    [Fact]
    public void Validate_DeviceWithoutChannels_ReportsOnlyTheMissingList()
    {
        // Arrange
        var device = DimmerDevice() with { Channels = null! };

        // Act
        var onItsOwn = _sut.Validate(device);
        var againstCatalog = _sut.Validate(device, TestCatalog());

        // Assert
        onItsOwn.CodesAndPaths().Should().Equal((ValidationCodes.NullEntry, "channels"));
        againstCatalog.CodesAndPaths().Should().Equal((ValidationCodes.NullEntry, "channels"));
    }

    [Fact]
    public void Validate_ChannelWithoutCapabilities_ReportsNoProfileViolationOrTemplateMismatch()
    {
        // Arrange
        var device = DimmerDevice();
        device = device with { Channels = [device.Channels[0], device.Channels[1] with { Capabilities = null! }] };

        // Act
        var errors = _sut.Validate(device, TestCatalog());

        // Assert
        errors.CodesAndPaths().Should().Equal((ValidationCodes.NullEntry, "channels[1].capabilities"));
    }

    [Fact]
    public void Validate_CapabilityWithoutFeatures_ReportsOnlyTheMissingList()
    {
        // Arrange
        var device = DimmerDevice(LevelCapability() with { Features = null! });

        // Act
        var errors = _sut.Validate(device, TestCatalog());

        // Assert
        errors.CodesAndPaths().Should().Equal((ValidationCodes.NullEntry, "channels[1].capabilities[0].features"));
    }

    [Fact]
    public void Validate_NullParameterValuesOfAnAlarm_ReportsNullEntryAtTheAlarm()
    {
        // Arrange
        var device = DimmerDevice(LevelCapability() with
        {
            AlarmParameters = new Dictionary<string, IReadOnlyDictionary<string, JsonNode?>> { ["highLimit"] = null! }
        });

        // Act
        var errors = _sut.Validate(device, TestCatalog());

        // Assert
        errors.CodesAndPaths().Should().Equal((ValidationCodes.NullEntry, "channels[1].capabilities[0].alarmParameters.highLimit"));
    }

    [Fact]
    public void Validate_OverrideOfAnAlarmWhoseDefinitionIsNull_ReportsNoUnknownAlarm()
    {
        // Arrange
        // The alarm is declared by the type, only its definition is null - the catalog's own problem.
        var level = Level();
        var catalog = TestCatalog() with
        {
            Capabilities = [level with { Alarms = new Dictionary<string, AlarmDef>(level.Alarms) { ["highLimit"] = null! } }]
        };

        // Act
        var errors = _sut.Validate(DimmerDevice(), catalog);

        // Assert
        errors.Should().BeEmpty();
    }

    [Fact]
    public void Validate_DeviceAgainstCatalogWithoutMandatoryLists_ReportsEveryReferenceAsUnresolved()
    {
        // Arrange
        var catalog = TestCatalog() with { Capabilities = null!, Profiles = null!, DeviceTypes = null! };

        // Act
        var errors = _sut.Validate(DimmerDevice(), catalog);

        // Assert
        errors.CodesAndPaths().Should().Equal(
            (ValidationCodes.UnresolvedType, "channels[1].capabilities[0].typeRef"),
            (ValidationCodes.UnresolvedType, "channels[1].profile"),
            (ValidationCodes.UnresolvedType, "typeRef"));
    }

    [Theory]
    [InlineData("from")]
    [InlineData("to")]
    public void Validate_MigrationWithUnnamedReference_ReportsOnlyTheNullEntry(string member)
    {
        // Arrange
        var migration = member == "from"
            ? new CapabilityMigration(default, new TypeRef("core.level", 2))
            : new CapabilityMigration(LevelRef, default);

        // Act
        var errors = _sut.Validate(migration);

        // Assert
        errors.CodesAndPaths().Should().Equal((ValidationCodes.NullEntry, member));
    }
}
