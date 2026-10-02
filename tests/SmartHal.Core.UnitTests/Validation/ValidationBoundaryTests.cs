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
/// Verifies the boundaries of the rules: version and count limits that are just allowed, non-finite numbers,
/// negative counts, the prescribed features of a template and the data types of command results and alarm
/// parameters. The struct depth through references is covered by <see cref="ReferenceDepthTests"/>.
/// </summary>
public sealed class ValidationBoundaryTests
{
    private readonly ContractValidator _sut = new();

    [Fact]
    public void Validate_HigherMinorListedBeforeTheLower_ResolvesToTheHigherMinor()
    {
        // Arrange
        var catalog = TestCatalog() with
        {
            Capabilities = [Level(), Level() with { Version = new TypeVersion(1, 0) }, Level() with { Version = new TypeVersion(2, 0) }]
        };
        var device = DimmerDevice(LevelCapability() with { Version = new TypeVersion(1, 2) });

        // Act
        var errors = _sut.Validate(device, catalog);

        // Assert
        errors.Should().BeEmpty();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void Validate_CapabilityMinorBelowTheCatalogMinor_ReportsNothing(int minor)
    {
        // Arrange
        var device = DimmerDevice(LevelCapability() with { Version = new TypeVersion(1, minor) });

        // Act
        var errors = _sut.Validate(device, TestCatalog());

        // Assert
        errors.Should().BeEmpty();
    }

    [Fact]
    public void Validate_ChannelWithCapabilityCountEqualToProfileMax_ReportsNothing()
    {
        // Arrange
        var device = DimmerDevice(LevelCapability(), LevelCapability() with { Key = "level2" });

        // Act
        var errors = _sut.Validate(device, TestCatalog());

        // Assert
        errors.Should().BeEmpty();
    }

    [Fact]
    public void Validate_RequiredProfileCapabilityBelowItsMinimum_ReportsProfileViolation()
    {
        // Arrange
        var catalog = TestCatalog() with
        {
            Profiles = [DimmerProfile() with { Capabilities = [new ProfileCapability(LevelRef, Required: true, Min: 2)] }]
        };

        // Act
        var errors = _sut.Validate(DimmerDevice(), catalog);

        // Assert
        errors.CodesAndPaths().Should().Equal((ValidationCodes.ProfileViolation, "channels[1]"));
        errors[0].Message.Should().Contain("at least 2");
    }

    [Fact]
    public void Validate_VirtualDeviceWithDeviceType_IsStillCheckedAgainstTheTemplate()
    {
        // Arrange
        var device = DimmerDevice();
        device = device with { Virtual = true, Channels = [device.Channels[0]] };

        // Act
        var errors = _sut.Validate(device, TestCatalog());

        // Assert
        errors.CodesAndPaths().Should().Equal((ValidationCodes.TemplateMismatch, "channels"));
    }

    [Fact]
    public void Validate_DeviceWithTwoRootChannels_ReportsRootChannelAndDuplicateKey()
    {
        // Arrange
        var device = DimmerDevice();
        device = device with { Channels = [.. device.Channels, new Channel(Guid.NewGuid(), "0", [])] };

        // Act
        var errors = _sut.Validate(device);

        // Assert
        errors.CodesAndPaths().Should().Equal(
            (ValidationCodes.RootChannel, "channels"),
            (ValidationCodes.DuplicateKey, "channels[2].key"));
    }

    [Fact]
    public void Validate_CatalogWithDuplicateDataTypeAndDeviceType_ReportsDuplicateKey()
    {
        // Arrange
        var catalog = TestCatalog() with { DataTypes = [Hsv(), Hsv()], DeviceTypes = [Dimmer(), Dimmer()] };

        // Act
        var errors = _sut.Validate(catalog);

        // Assert
        errors.CodesAndPaths().Should().Equal(
            (ValidationCodes.DuplicateKey, "dataTypes[1].name"),
            (ValidationCodes.DuplicateKey, "deviceTypes[1].name"));
    }

    public static TheoryData<NumberType, string, string> NonFiniteNumbers => new()
    {
        { new NumberType(Minimum: double.NaN, Maximum: 1), ValidationCodes.InvalidRange, "dataType.fields.h.minimum" },
        { new NumberType(Minimum: 0, Maximum: double.PositiveInfinity), ValidationCodes.InvalidRange, "dataType.fields.h.maximum" },
        { new NumberType(Minimum: double.NegativeInfinity), ValidationCodes.InvalidRange, "dataType.fields.h.minimum" },
        { new NumberType(Step: double.NaN), ValidationCodes.InvalidStep, "dataType.fields.h.step" },
        { new NumberType(Step: double.PositiveInfinity), ValidationCodes.InvalidStep, "dataType.fields.h.step" }
    };

    [Theory]
    [MemberData(nameof(NonFiniteNumbers))]
    public void Validate_NumericTypeWithNonFiniteConstraint_ReportsTheConstraint(NumberType number, string code, string path)
    {
        // Arrange
        var hsv = Hsv() with { DataType = new ObjectType(new Dictionary<string, DataType> { ["h"] = number }) };

        // Act
        var errors = _sut.Validate(hsv);

        // Assert
        errors.CodesAndPaths().Should().Equal((code, path));
    }

    [Theory]
    [InlineData(-1, 2, "capabilities[0].min")]
    [InlineData(null, -1, "capabilities[0].max")]
    [InlineData(1, -2, "capabilities[0].max")]
    public void Validate_ProfileCapabilityWithNegativeCount_ReportsInvalidRange(int? min, int? max, string path)
    {
        // Arrange
        var profile = DimmerProfile() with { Capabilities = [new ProfileCapability(LevelRef, Required: true, min, max)] };

        // Act
        var errors = _sut.Validate(profile);

        // Assert
        errors.CodesAndPaths().Should().Equal((ValidationCodes.InvalidRange, path));
    }

    [Fact]
    public void Validate_CapabilityLackingAFeaturePrescribedByTheTemplate_ReportsTemplateMismatch()
    {
        // Arrange
        var dimmer = Dimmer();
        var catalog = TestCatalog() with
        {
            DeviceTypes =
            [
                dimmer with
                {
                    Channels =
                    [
                        dimmer.Channels[0],
                        dimmer.Channels[1] with { Capabilities = [new CapabilityTemplate("level", LevelRef, ["hsv"])] }
                    ]
                }
            ]
        };
        var device = DimmerDevice(LevelCapability() with { Features = [] });

        // Act
        var errors = _sut.Validate(device, catalog);

        // Assert
        errors.CodesAndPaths().Should().Equal((ValidationCodes.TemplateMismatch, "channels[1].capabilities[0].features"));
        errors[0].Message.Should().Contain("'hsv'");
    }

    [Fact]
    public void Validate_CapabilityWithThePrescribedFeature_ReportsNothing()
    {
        // Arrange
        var dimmer = Dimmer();
        var catalog = TestCatalog() with
        {
            DeviceTypes =
            [
                dimmer with
                {
                    Channels =
                    [
                        dimmer.Channels[0],
                        dimmer.Channels[1] with { Capabilities = [new CapabilityTemplate("level", LevelRef, ["hsv"])] }
                    ]
                }
            ]
        };

        // Act
        var errors = _sut.Validate(DimmerDevice(), catalog);

        // Assert
        errors.Should().BeEmpty();
    }

    [Fact]
    public void Validate_CommandResultNestedTooDeep_ReportsStructTooDeep()
    {
        // Arrange
        var level = Level();
        var deep = new ObjectType(new Dictionary<string, DataType>
        {
            ["a"] = new ObjectType(new Dictionary<string, DataType>
            {
                ["b"] = new ObjectType(new Dictionary<string, DataType> { ["c"] = new BooleanType() })
            })
        });
        var calibrate = level.Commands["calibrate"] with { Result = deep };
        var type = level with { Commands = new Dictionary<string, CommandDef>(level.Commands) { ["calibrate"] = calibrate } };

        // Act
        var errors = _sut.Validate(type);

        // Assert
        errors.CodesAndPaths().Should().Equal((ValidationCodes.StructTooDeep, "commands.calibrate.result.fields.a.fields.b"));
    }

    [Fact]
    public void Validate_AlarmParameterWithContradictingRange_ReportsInvalidRange()
    {
        // Arrange
        var level = Level();
        var highLimit = level.Alarms["highLimit"] with
        {
            Parameters = new Dictionary<string, AlarmParameter> { ["limit"] = new(new NumberType(Minimum: 5, Maximum: 1)) }
        };
        var type = level with { Alarms = new Dictionary<string, AlarmDef>(level.Alarms) { ["highLimit"] = highLimit } };

        // Act
        var errors = _sut.Validate(type);

        // Assert
        errors.CodesAndPaths().Should().Equal((ValidationCodes.InvalidRange, "alarms.highLimit.parameters.limit.dataType.minimum"));
    }
}
