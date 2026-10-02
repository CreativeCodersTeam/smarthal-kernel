using System.Text.Json.Nodes;
using AwesomeAssertions;
using SmartHal.Contracts.Primitives;
using SmartHal.Contracts.Schema;
using SmartHal.Contracts.Topology;
using SmartHal.Core.Abstractions.Validation;
using SmartHal.Core.Validation;
using Xunit;
using static SmartHal.Core.UnitTests.Validation.ValidationFixtures;

namespace SmartHal.Core.UnitTests.Validation;

/// <summary>
/// Verifies the validation of a device against a type catalog: reference resolution (R11), versions and features
/// (R12), device type templates (R13), channel profiles (R14) and instance overrides (R15).
/// </summary>
public sealed class DeviceCatalogValidationTests
{
    private readonly ContractValidator _sut = new();

    [Fact]
    public void Validate_UnresolvedDeviceType_ReportsUnresolvedTypeAndSkipsTheTemplateCheck()
    {
        // Arrange
        var device = DimmerDevice() with { TypeRef = new TypeRef("acme.dimmer", 2) };

        // Act
        var errors = _sut.Validate(device, TestCatalog());

        // Assert
        errors.CodesAndPaths().Should().Equal((ValidationCodes.UnresolvedType, "typeRef"));
    }

    [Fact]
    public void Validate_UnresolvedProfileAndCapabilityType_ReportsUnresolvedType()
    {
        // Arrange
        var device = DimmerDevice(LevelCapability() with { TypeRef = new TypeRef("core.level", 7), Version = new TypeVersion(7, 0) });
        device = device with
        {
            Channels = [device.Channels[0], device.Channels[1] with { Profile = new TypeRef("core.profile.unknown", 1) }]
        };

        // Act
        var errors = _sut.Validate(device, TestCatalog());

        // Assert
        errors.CodesAndPaths().Should().Equal(
            (ValidationCodes.UnresolvedType, "channels[1].capabilities[0].typeRef"),
            (ValidationCodes.UnresolvedType, "channels[1].profile"),
            (ValidationCodes.TemplateMismatch, "channels[1].capabilities[0].typeRef"));
    }

    [Fact]
    public void Validate_CapabilityOnHighestCatalogMinor_ReportsNothing()
    {
        // Arrange
        // The catalog holds core.level 1.0 and 1.2; the reference resolves to the highest minor, so 1.2 is valid.
        var device = DimmerDevice(LevelCapability() with { Version = new TypeVersion(1, 2) });

        // Act
        var errors = _sut.Validate(device, TestCatalogWithTwoMinors());

        // Assert
        errors.Should().BeEmpty();
    }

    [Theory]
    [InlineData(1, 3)]
    [InlineData(2, 0)]
    public void Validate_CapabilityVersionNotMatchingReferenceOrCatalog_ReportsVersionMismatch(int major, int minor)
    {
        // Arrange
        var device = DimmerDevice(LevelCapability() with { Version = new TypeVersion(major, minor) });

        // Act
        var errors = _sut.Validate(device, TestCatalog());

        // Assert
        errors.CodesAndPaths().Should().Equal((ValidationCodes.VersionMismatch, "channels[1].capabilities[0].version"));
    }

    [Fact]
    public void Validate_CapabilityWithUndeclaredFeature_ReportsUnknownFeature()
    {
        // Arrange
        var device = DimmerDevice(LevelCapability() with { Features = ["hsv", "ct"] });

        // Act
        var errors = _sut.Validate(device, TestCatalog());

        // Assert
        errors.CodesAndPaths().Should().Equal((ValidationCodes.UnknownFeature, "channels[1].capabilities[0].features[1]"));
    }

    [Fact]
    public void Validate_DeviceMissingATemplateChannel_ReportsTemplateMismatch()
    {
        // Arrange
        var device = DimmerDevice();
        device = device with { Channels = [device.Channels[0]] };

        // Act
        var errors = _sut.Validate(device, TestCatalog());

        // Assert
        errors.CodesAndPaths().Should().Equal((ValidationCodes.TemplateMismatch, "channels"));
        errors[0].Message.Should().Contain("'1'");
    }

    [Fact]
    public void Validate_ChannelMissingATemplateCapability_ReportsTemplateMismatchAndProfileViolation()
    {
        // Arrange
        var device = DimmerDevice();
        device = device with { Channels = [device.Channels[0], device.Channels[1] with { Capabilities = [] }] };

        // Act
        var errors = _sut.Validate(device, TestCatalog());

        // Assert
        errors.CodesAndPaths().Should().Equal(
            (ValidationCodes.ProfileViolation, "channels[1]"),
            (ValidationCodes.TemplateMismatch, "channels[1].capabilities"));
    }

    [Fact]
    public void Validate_CapabilityOfOtherTypeThanTheTemplate_ReportsTemplateMismatch()
    {
        // Arrange
        var device = DimmerDevice(LevelCapability() with { TypeRef = new TypeRef("core.level", 2), Version = new TypeVersion(2, 0) });
        device = device with { Channels = [device.Channels[0], device.Channels[1] with { Profile = null }] };

        // Act
        var errors = _sut.Validate(device, TestCatalog());

        // Assert
        errors.CodesAndPaths().Should().Equal((ValidationCodes.TemplateMismatch, "channels[1].capabilities[0].typeRef"));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(3)]
    public void Validate_VirtualChannelOutsideItsProfileCounts_ReportsProfileViolation(int count)
    {
        // Arrange
        var capabilities = Enumerable.Range(0, count).Select(i => LevelCapability() with { Key = $"level{i}" }).ToArray();
        var device = new Device(
            Guid.NewGuid(),
            "flur.alle",
            "Flur alle",
            Virtual: true,
            DeviceLifecycle.Active,
            [new Channel(Guid.NewGuid(), "0", []), new Channel(Guid.NewGuid(), "1", capabilities, DimmerProfileRef)]);

        // Act
        var errors = _sut.Validate(device, TestCatalog());

        // Assert
        errors.CodesAndPaths().Should().Equal((ValidationCodes.ProfileViolation, "channels[1]"));
    }

    [Fact]
    public void Validate_OptionalProfileCapabilityAbsentOrBelowMinimum_ReportsOnlyTheShortfall()
    {
        // Arrange
        var profile = DimmerProfile() with
        {
            Capabilities =
            [
                new ProfileCapability(LevelRef, Required: false, Min: 2),
                new ProfileCapability(new TypeRef("core.level", 2), Required: false)
            ]
        };
        var catalog = TestCatalog() with { Profiles = [profile] };
        var device = DimmerDevice() with { Virtual = true, TypeRef = null };

        // Act
        var errors = _sut.Validate(device, catalog);

        // Assert
        errors.CodesAndPaths().Should().Equal((ValidationCodes.ProfileViolation, "channels[1]"));
        errors[0].Message.Should().Contain("at least 2");
    }

    [Fact]
    public void Validate_OverridesNamingUnknownElements_ReportsUnknownPropertyAlarmAndParameter()
    {
        // Arrange
        var capability = LevelCapability() with
        {
            HistoryOverrides = new Dictionary<string, HistoryPolicy> { ["level"] = new(TimeSpan.FromDays(1)),
                ["speed"] = new(TimeSpan.FromDays(1)) },
            AlarmParameters = new Dictionary<string, IReadOnlyDictionary<string, JsonNode?>>
            {
                ["highLimit"] = new Dictionary<string, JsonNode?> { ["limit"] = 80, ["delay"] = "PT10S" },
                ["lowLimit"] = new Dictionary<string, JsonNode?> { ["limit"] = 5 }
            }
        };

        // Act
        var errors = _sut.Validate(DimmerDevice(capability), TestCatalog());

        // Assert
        errors.CodesAndPaths().Should().Equal(
            (ValidationCodes.UnknownProperty, "channels[1].capabilities[0].historyOverrides.speed"),
            (ValidationCodes.UnknownAlarmParameter, "channels[1].capabilities[0].alarmParameters.highLimit.delay"),
            (ValidationCodes.UnknownAlarm, "channels[1].capabilities[0].alarmParameters.lowLimit"));
    }

    [Fact]
    public void Validate_ParametersForAnAlarmWithoutParameters_ReportsUnknownAlarmParameter()
    {
        // Arrange
        var capability = LevelCapability() with
        {
            AlarmParameters = new Dictionary<string, IReadOnlyDictionary<string, JsonNode?>>
            {
                ["blockedAlarm"] = new Dictionary<string, JsonNode?> { ["limit"] = 1 }
            }
        };

        // Act
        var errors = _sut.Validate(DimmerDevice(capability), TestCatalog());

        // Assert
        errors.CodesAndPaths().Should().Equal(
            (ValidationCodes.UnknownAlarmParameter, "channels[1].capabilities[0].alarmParameters.blockedAlarm.limit"));
    }

    [Fact]
    public void Validate_CatalogWithNullEntries_DoesNotThrowWhileResolving()
    {
        // Arrange
        var catalog = TestCatalog();
        catalog = catalog with { Capabilities = [null!, .. catalog.Capabilities], DeviceTypes = [null!, .. catalog.DeviceTypes] };

        // Act
        var errors = _sut.Validate(DimmerDevice(), catalog);

        // Assert
        errors.Should().BeEmpty("null catalog entries are the catalog's own problem and are skipped when resolving");
    }
}
