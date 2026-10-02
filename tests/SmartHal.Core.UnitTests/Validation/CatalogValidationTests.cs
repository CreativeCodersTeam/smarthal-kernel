using AwesomeAssertions;
using SmartHal.Contracts.Primitives;
using SmartHal.Contracts.Schema;
using SmartHal.Core.Abstractions.Validation;
using SmartHal.Core.Validation;
using Xunit;
using static SmartHal.Core.UnitTests.Validation.ValidationFixtures;

namespace SmartHal.Core.UnitTests.Validation;

/// <summary>
/// Verifies the validation of a whole type catalog: entry rules with prefixed paths, duplicates, references between
/// the entries (R11) and the profiles of channel templates (R14).
/// </summary>
public sealed class CatalogValidationTests
{
    private readonly ContractValidator _sut = new ContractValidator();

    [Fact]
    public void Validate_EntryWithViolation_ReportsItWithTheListPrefix()
    {
        // Arrange
        var catalog = TestCatalog();
        var broken = Level() with
        {
            Name = "core.broken",
            Commands = new Dictionary<string, CommandDef> { ["calibrate"] = new CommandDef(Completion.Result, TimeSpan.FromSeconds(1)) }
        };
        catalog = catalog with { Capabilities = [catalog.Capabilities[0], broken, catalog.Capabilities[1]] };

        // Act
        var errors = _sut.Validate(catalog);

        // Assert
        errors.CodesAndPaths().Should().Equal((ValidationCodes.MissingResult, "capabilities[1].commands.calibrate.result"));
    }

    [Fact]
    public void Validate_TwoMinorsOfTheSameMajor_ReportsDuplicateKey()
    {
        // Arrange
        var catalog = TestCatalogWithTwoMinors();

        // Act
        var errors = _sut.Validate(catalog);

        // Assert
        errors.CodesAndPaths().Should().Equal((ValidationCodes.DuplicateKey, "capabilities[1].name"));
    }

    [Fact]
    public void Validate_SameTypeAndMajorTwice_ReportsDuplicateKey()
    {
        // Arrange
        var catalog = TestCatalog();
        catalog = catalog with { Profiles = [DimmerProfile(), DimmerProfile() with { Version = new TypeVersion(1, 1) }] };

        // Act
        var errors = _sut.Validate(catalog);

        // Assert
        errors.CodesAndPaths().Should().Equal((ValidationCodes.DuplicateKey, "profiles[1].name"));
    }

    [Fact]
    public void Validate_UnresolvedReusableDataType_ReportsUnresolvedType()
    {
        // Arrange
        var catalog = TestCatalog() with { DataTypes = null };

        // Act
        var errors = _sut.Validate(catalog);

        // Assert
        errors.CodesAndPaths().Should().Equal(
            (ValidationCodes.UnresolvedType, "capabilities[0].properties.color.dataType.ref"),
            (ValidationCodes.UnresolvedType, "capabilities[1].properties.color.dataType.ref"));
    }

    [Fact]
    public void Validate_UnresolvedCapabilityTypes_ReportsEveryReferenceThatPointsToThem()
    {
        // Arrange
        var catalog = TestCatalog();
        catalog = catalog with { Capabilities = [catalog.Capabilities[1]] };

        // Act
        var errors = _sut.Validate(catalog);

        // Assert
        errors.CodesAndPaths().Should().Equal(
            (ValidationCodes.UnresolvedType, "profiles[0].capabilities[0].type"),
            (ValidationCodes.UnresolvedType, "deviceTypes[0].channels[1].capabilities[0].type"),
            (ValidationCodes.UnresolvedType, "migrations[0].from"));
    }

    [Fact]
    public void Validate_UnresolvedProfileAndMigrationTarget_ReportsUnresolvedType()
    {
        // Arrange
        var catalog = TestCatalog() with
        {
            Profiles = [],
            Migrations = [new CapabilityMigration(LevelRef, new TypeRef("core.level", 3))]
        };

        // Act
        var errors = _sut.Validate(catalog);

        // Assert
        errors.CodesAndPaths().Should().Equal(
            (ValidationCodes.UnresolvedType, "deviceTypes[0].channels[1].profile"),
            (ValidationCodes.UnresolvedType, "migrations[0].to"));
    }

    [Fact]
    public void Validate_ChannelTemplateViolatingItsProfile_ReportsProfileViolation()
    {
        // Arrange
        var dimmer = Dimmer();
        var crowded = new ChannelTemplate(
            "1",
            [new CapabilityTemplate("a", LevelRef), new CapabilityTemplate("b", LevelRef), new CapabilityTemplate("c", LevelRef)],
            DimmerProfileRef);
        var catalog = TestCatalog() with { DeviceTypes = [dimmer with { Channels = [dimmer.Channels[0], crowded] }] };

        // Act
        var errors = _sut.Validate(catalog);

        // Assert
        errors.CodesAndPaths().Should().Equal((ValidationCodes.ProfileViolation, "deviceTypes[0].channels[1]"));
        errors[0].Message.Should().Contain("at most 2");
    }

    [Fact]
    public void Validate_NullEntriesAndNullMandatoryList_ReportsNullEntry()
    {
        // Arrange
        var catalog = TestCatalog() with { Profiles = null!, DeviceTypes = [null!] };

        // Act
        var errors = _sut.Validate(catalog);

        // Assert
        errors.CodesAndPaths().Should().Contain((ValidationCodes.NullEntry, "profiles"))
            .And.Contain((ValidationCodes.NullEntry, "deviceTypes[0]"));
        errors.Select(error => error.Code).Should().OnlyContain(code => code == ValidationCodes.NullEntry);
    }

    [Fact]
    public void Validate_CatalogWithoutMigrations_ReportsNothing()
    {
        // Arrange
        var catalog = TestCatalog() with { Migrations = null };

        // Act
        var errors = _sut.Validate(catalog);

        // Assert
        errors.Should().BeEmpty();
    }
}
