using SmartHal.Contracts.Primitives;

namespace SmartHal.Contracts.Schema;

/// <summary>
/// Describes a channel a device type creates.
/// </summary>
/// <param name="Key">The key of the channel within the device; <c>0</c> is the root channel every device has.</param>
/// <param name="Capabilities">The capabilities of the channel.</param>
/// <param name="Profile">The channel profile the channel follows; <see langword="null"/> when it follows none.</param>
public sealed record ChannelTemplate(
    string Key,
    IReadOnlyList<CapabilityTemplate> Capabilities,
    TypeRef? Profile = null);
