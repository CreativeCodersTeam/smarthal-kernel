using AwesomeAssertions;
using SmartHal.Contracts.Api;
using SmartHal.Contracts.DataTypes;
using SmartHal.Contracts.Integration;
using SmartHal.Contracts.Primitives;
using SmartHal.Contracts.Schema;
using SmartHal.Core.Abstractions.Validation;
using SmartHal.Core.Validation;
using Xunit;
using static SmartHal.Core.UnitTests.Validation.ValidationFixtures;

namespace SmartHal.Core.UnitTests.Validation;

/// <summary>
/// Verifies the rules of profiles (R7), device types (R8) and migrations (R10) on their own.
/// </summary>
public sealed class SchemaRulesTests
{
    private readonly ContractValidator _sut = new();

    [Fact]
    public void Validate_DataTypeDefWithoutName_ReportsNullEntryAtTheName()
    {
        // Arrange
        var definition = new DataTypeDef(null!, new TypeVersion(1, 0), new BooleanType());

        // Act
        var errors = _sut.Validate(definition);

        // Assert
        errors.CodesAndPaths().Should().Equal((ValidationCodes.NullEntry, "name"));
    }

    [Fact]
    public void Validate_CatalogWithUnnamedDataTypeDef_ReportsNullEntryAtItsName()
    {
        // Arrange
        var catalog = new TypeCatalog([], [], [], [new DataTypeDef(null!, new TypeVersion(1, 0), new BooleanType())]);

        // Act
        var errors = _sut.Validate(catalog);

        // Assert
        errors.CodesAndPaths().Should().Equal((ValidationCodes.NullEntry, "dataTypes[0].name"));
    }

    [Fact]
    public void Validate_CatalogWithTwoUnnamedDataTypeDefsOfTheSameMajor_ReportsOnlyTheMissingNames()
    {
        // Arrange
        var catalog = new TypeCatalog(
            [],
            [],
            [],
            [
                new DataTypeDef(null!, new TypeVersion(1, 0), new BooleanType()),
                new DataTypeDef(null!, new TypeVersion(1, 2), new BooleanType())
            ]);

        // Act
        var errors = _sut.Validate(catalog);

        // Assert
        errors.CodesAndPaths().Should().Equal(
            (ValidationCodes.NullEntry, "dataTypes[0].name"),
            (ValidationCodes.NullEntry, "dataTypes[1].name"));
    }

    [Fact]
    public void Validate_NamedDataTypeDef_ReportsNothing()
    {
        // Arrange
        var definition = new DataTypeDef("core.types.flag", new TypeVersion(1, 0), new BooleanType());

        // Act
        var errors = _sut.Validate(definition);

        // Assert
        errors.Should().BeEmpty();
    }

    [Fact]
    public void Validate_ProfileCapabilityWithMinimumAboveMaximum_ReportsInvalidRange()
    {
        // Arrange
        var profile = DimmerProfile() with { Capabilities = [new ProfileCapability(LevelRef, true, Min: 3, Max: 2)] };

        // Act
        var errors = _sut.Validate(profile);

        // Assert
        errors.CodesAndPaths().Should().Equal((ValidationCodes.InvalidRange, "capabilities[0].min"));
    }

    [Fact]
    public void Validate_ProfileCapabilityWithOnlyOneBound_ReportsNothing()
    {
        // Arrange
        var profile = DimmerProfile() with { Capabilities = [new ProfileCapability(LevelRef, false, Min: 3)] };

        // Act
        var errors = _sut.Validate(profile);

        // Assert
        errors.Should().BeEmpty();
    }

    [Fact]
    public void Validate_DeviceTypeWithoutRootChannel_ReportsRootChannel()
    {
        // Arrange
        var deviceType = Dimmer() with { Channels = [Dimmer().Channels[1]] };

        // Act
        var errors = _sut.Validate(deviceType);

        // Assert
        errors.CodesAndPaths().Should().Equal((ValidationCodes.RootChannel, "channels"));
    }

    [Fact]
    public void Validate_DeviceTypeWithTwoRootChannels_ReportsRootChannelAndDuplicateKey()
    {
        // Arrange
        var dimmer = Dimmer();
        var deviceType = dimmer with { Channels = [.. dimmer.Channels, new ChannelTemplate("0", [])] };

        // Act
        var errors = _sut.Validate(deviceType);

        // Assert
        errors.CodesAndPaths().Should().Equal(
            (ValidationCodes.RootChannel, "channels"),
            (ValidationCodes.DuplicateKey, "channels[2].key"));
    }

    [Fact]
    public void Validate_DeviceTypeWithDuplicateCapabilityKey_ReportsDuplicateKey()
    {
        // Arrange
        var dimmer = Dimmer();
        var deviceType = dimmer with
        {
            Channels =
            [
                dimmer.Channels[0],
                new ChannelTemplate("1", [new CapabilityTemplate("level", LevelRef), new CapabilityTemplate("level", LevelRef)])
            ]
        };

        // Act
        var errors = _sut.Validate(deviceType);

        // Assert
        errors.CodesAndPaths().Should().Equal((ValidationCodes.DuplicateKey, "channels[1].capabilities[1].key"));
    }

    [Fact]
    public void Validate_DeviceTypeWithSameCapabilityKeyInTwoChannels_ReportsNothing()
    {
        // Arrange
        var dimmer = Dimmer();
        var deviceType = dimmer with
        {
            Channels = [.. dimmer.Channels, new ChannelTemplate("2", [new CapabilityTemplate("level", LevelRef)])]
        };

        // Act
        var errors = _sut.Validate(deviceType);

        // Assert
        errors.Should().BeEmpty("capability keys are unique per channel only");
    }

    [Fact]
    public void Validate_BindingTemplateWithInvalidParameterAndNullMapping_ReportsBoth()
    {
        // Arrange
        var deviceType = Dimmer() with
        {
            BindingTemplates =
            [
                new BindingTemplate(
                    Protocols.Modbus,
                    new Dictionary<string, DataType> { ["slaveId"] = new IntegerType(Minimum: 248, Maximum: 1) },
                    [null!])
            ]
        };

        // Act
        var errors = _sut.Validate(deviceType);

        // Assert
        errors.CodesAndPaths().Should().Equal(
            (ValidationCodes.InvalidRange, "bindingTemplates[0].parameters.slaveId.minimum"),
            (ValidationCodes.NullEntry, "bindingTemplates[0].mappings[0]"));
    }

    [Fact]
    public void Validate_DeviceTypeWithNullChannel_ReportsNullEntryAndStillFindsTheRoot()
    {
        // Arrange
        var dimmer = Dimmer();
        var deviceType = dimmer with { Channels = [dimmer.Channels[0], null!] };

        // Act
        var errors = _sut.Validate(deviceType);

        // Assert
        errors.CodesAndPaths().Should().Equal((ValidationCodes.NullEntry, "channels[1]"));
    }

    [Theory]
    [InlineData("core.level", 1, "core.dimmer", 2)]
    [InlineData("core.level", 2, "core.level", 2)]
    [InlineData("core.level", 2, "core.level", 1)]
    public void Validate_MigrationThatDoesNotLeadToAHigherMajorOfTheSameType_ReportsInvalidMigration(
        string fromName,
        int fromMajor,
        string toName,
        int toMajor)
    {
        // Arrange
        var migration = new CapabilityMigration(new TypeRef(fromName, fromMajor), new TypeRef(toName, toMajor));

        // Act
        var errors = _sut.Validate(migration);

        // Assert
        errors.CodesAndPaths().Should().Equal((ValidationCodes.InvalidMigration, string.Empty));
    }

    [Fact]
    public void Validate_MigrationToOtherTypeAndLowerMajor_ReportsBothProblems()
    {
        // Arrange
        var migration = new CapabilityMigration(new TypeRef("core.level", 2), new TypeRef("core.dimmer", 1));

        // Act
        var errors = _sut.Validate(migration);

        // Assert
        errors.CodesAndPaths().Should().Equal(
            (ValidationCodes.InvalidMigration, string.Empty),
            (ValidationCodes.InvalidMigration, string.Empty));
    }
}
