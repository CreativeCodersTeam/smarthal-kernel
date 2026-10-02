using AwesomeAssertions;
using SmartHal.Contracts.Topology;
using SmartHal.Core.Abstractions.Validation;
using SmartHal.Core.Validation;
using Xunit;
using static SmartHal.Core.UnitTests.Validation.ValidationFixtures;

namespace SmartHal.Core.UnitTests.Validation;

/// <summary>
/// Verifies the rules of a device on its own: keys and root channel (R8), device type (R9) and null entries.
/// </summary>
public sealed class DeviceRulesTests
{
    private readonly ContractValidator _sut = new ContractValidator();

    [Fact]
    public void Validate_RealDeviceWithoutDeviceType_ReportsMissingDeviceType()
    {
        // Arrange
        var device = DimmerDevice() with { TypeRef = null };

        // Act
        var errors = _sut.Validate(device);

        // Assert
        errors.CodesAndPaths().Should().Equal((ValidationCodes.MissingDeviceType, "typeRef"));
    }

    [Fact]
    public void Validate_VirtualDeviceWithoutDeviceType_ReportsNothing()
    {
        // Arrange
        var device = DimmerDevice() with { Virtual = true, TypeRef = null };

        // Act
        var errors = _sut.Validate(device);

        // Assert
        errors.Should().BeEmpty();
    }

    [Fact]
    public void Validate_DeviceWithoutRootChannel_ReportsRootChannel()
    {
        // Arrange
        var device = DimmerDevice();
        device = device with { Channels = [device.Channels[1]] };

        // Act
        var errors = _sut.Validate(device);

        // Assert
        errors.CodesAndPaths().Should().Equal((ValidationCodes.RootChannel, "channels"));
    }

    [Fact]
    public void Validate_DeviceWithDuplicateChannelAndCapabilityKeys_ReportsDuplicateKeys()
    {
        // Arrange
        var device = DimmerDevice(LevelCapability(), LevelCapability());
        device = device with { Channels = [.. device.Channels, new Channel(Guid.NewGuid(), "1", [])] };

        // Act
        var errors = _sut.Validate(device);

        // Assert
        errors.CodesAndPaths().Should().Equal(
            (ValidationCodes.DuplicateKey, "channels[2].key"),
            (ValidationCodes.DuplicateKey, "channels[1].capabilities[1].key"));
    }

    [Fact]
    public void Validate_DeviceWithNullEntries_ReportsNullEntryWithoutThrowing()
    {
        // Arrange
        var capability = LevelCapability() with
        {
            Features = [null!],
            HistoryOverrides = new Dictionary<string, Contracts.Schema.HistoryPolicy> { ["level"] = null! }
        };
        var device = DimmerDevice(capability);
        device = device with { Channels = [.. device.Channels, null!] };

        // Act
        var errors = _sut.Validate(device);

        // Assert
        errors.CodesAndPaths().Should().Equal(
            (ValidationCodes.NullEntry, "channels[2]"),
            (ValidationCodes.NullEntry, "channels[1].capabilities[0].features[0]"),
            (ValidationCodes.NullEntry, "channels[1].capabilities[0].historyOverrides.level"));
    }

    [Fact]
    public void Validate_DeviceWithoutCatalog_DoesNotResolveReferences()
    {
        // Arrange
        var device = DimmerDevice() with { TypeRef = new Contracts.Primitives.TypeRef("acme.unknown", 1) };

        // Act
        var errors = _sut.Validate(device);

        // Assert
        errors.Should().BeEmpty();
    }
}
