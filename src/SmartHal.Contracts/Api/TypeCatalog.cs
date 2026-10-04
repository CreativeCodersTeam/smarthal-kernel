using SmartHal.Contracts.DataTypes;
using SmartHal.Contracts.Schema;

namespace SmartHal.Contracts.Api;

/// <summary>
/// Lists every schema type the kernel knows.
/// </summary>
/// <param name="Capabilities">The capability types.</param>
/// <param name="Profiles">The channel profiles.</param>
/// <param name="DeviceTypes">The device types.</param>
/// <param name="DataTypes">The reusable data types such as <c>core.types.hsv</c>; <see langword="null"/> when there are none.</param>
/// <param name="Migrations">The migrations between major versions of capability types; <see langword="null"/> when there are none.</param>
public sealed record TypeCatalog(
    IReadOnlyList<CapabilityType> Capabilities,
    IReadOnlyList<ChannelProfile> Profiles,
    IReadOnlyList<DeviceType> DeviceTypes,
    IReadOnlyList<DataTypeDef>? DataTypes = null,
    IReadOnlyList<CapabilityMigration>? Migrations = null);
