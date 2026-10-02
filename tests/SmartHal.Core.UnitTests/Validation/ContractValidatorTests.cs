using AwesomeAssertions;
using SmartHal.Contracts.Api;
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
/// Verifies the entry points of <see cref="ContractValidator"/>: valid objects pass, null arguments are rejected and
/// every violation of one object is reported in one run.
/// </summary>
public sealed class ContractValidatorTests
{
    private readonly ContractValidator _sut = new ContractValidator();

    [Fact]
    public void Validate_ValidFixtures_ReportNothing()
    {
        // Arrange
        var catalog = TestCatalog();

        // Act
        IReadOnlyList<ValidationError>[] results =
        [
            _sut.Validate(Level()),
            _sut.Validate(Hsv()),
            _sut.Validate(DimmerProfile()),
            _sut.Validate(Dimmer()),
            _sut.Validate(new CapabilityMigration(LevelRef, new TypeRef("core.level", 2))),
            _sut.Validate(DimmerDevice()),
            _sut.Validate(catalog),
            _sut.Validate(DimmerDevice(), catalog)
        ];

        // Assert
        results.Should().AllSatisfy(errors => errors.Should().BeEmpty());
    }

    public static TheoryData<string> NullArgumentCalls =>
    [
        "capabilityType", "dataTypeDef", "profile", "deviceType", "migration", "device", "catalog", "deviceWithCatalog", "catalogWithDevice"
    ];

    [Theory]
    [MemberData(nameof(NullArgumentCalls))]
    public void Validate_NullArgument_ThrowsArgumentNullException(string call)
    {
        // Arrange
        Action act = call switch
        {
            "capabilityType" => () => _sut.Validate((CapabilityType)null!),
            "dataTypeDef" => () => _sut.Validate((DataTypeDef)null!),
            "profile" => () => _sut.Validate((ChannelProfile)null!),
            "deviceType" => () => _sut.Validate((DeviceType)null!),
            "migration" => () => _sut.Validate((CapabilityMigration)null!),
            "device" => () => _sut.Validate((Device)null!),
            "catalog" => () => _sut.Validate((TypeCatalog)null!),
            "deviceWithCatalog" => () => _sut.Validate(null!, TestCatalog()),
            _ => () => _sut.Validate(DimmerDevice(), null!)
        };

        // Act

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Validate_CapabilityTypeWithSeveralViolations_ReportsAllOfThemInTraversalOrder()
    {
        // Arrange
        var level = Level();
        var type = level with
        {
            Properties = new Dictionary<string, PropertyDef>(level.Properties)
            {
                ["level"] = new PropertyDef(new NumberType("%", 100, 0), PropertyCategory.State, Feature: "fast")
            },
            Commands = new Dictionary<string, CommandDef>(level.Commands)
            {
                ["calibrate"] = new CommandDef(Completion.Result, TimeSpan.FromSeconds(10))
            }
        };

        // Act
        var errors = _sut.Validate(type);

        // Assert
        errors.CodesAndPaths().Should().Equal(
            (ValidationCodes.InvalidRange, "properties.level.dataType.minimum"),
            (ValidationCodes.UnknownFeature, "properties.level.feature"),
            (ValidationCodes.MissingResult, "commands.calibrate.result"));
        errors.Should().AllSatisfy(error => error.Message.Should().NotBeNullOrWhiteSpace());
    }

    [Fact]
    public void Validate_SameInstanceFromSeveralThreads_ReturnsIndependentResults()
    {
        // Arrange
        var invalid = Level() with { Features = null };

        // Act
        var results = Enumerable.Range(0, 32)
            .AsParallel()
            .Select(i => i % 2 == 0 ? _sut.Validate(Level()) : _sut.Validate(invalid))
            .ToList();

        // Assert
        results.Should().AllSatisfy(errors => errors.Count.Should().BeOneOf(0, 1));
        results.Count(errors => errors.Count == 1).Should().Be(16);
    }
}
