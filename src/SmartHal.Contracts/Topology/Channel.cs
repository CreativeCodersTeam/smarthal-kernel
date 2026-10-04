using SmartHal.Contracts.Primitives;

namespace SmartHal.Contracts.Topology;

/// <summary>
/// Describes a channel: a functional sub-unit of a device.
/// </summary>
/// <param name="Id">The immutable id of the channel.</param>
/// <param name="Key">The key of the channel, unique within the device, for example <c>0</c> or <c>motor</c>.</param>
/// <param name="Capabilities">The capabilities of the channel.</param>
/// <param name="Profile">The channel profile the channel follows; <see langword="null"/> when it follows none.</param>
/// <param name="LocationId">The id of a location that overrides the location of the device; <see langword="null"/> when the device location
/// applies.</param>
/// <param name="Aliases">The former keys, valid for a limited time; <see langword="null"/> when there are none.</param>
/// <param name="Tags">Free key/value tags the kernel does not check; <see langword="null"/> when there are none.</param>
public sealed record Channel(
    Guid Id,
    string Key,
    IReadOnlyList<Capability> Capabilities,
    TypeRef? Profile = null,
    Guid? LocationId = null,
    IReadOnlyList<string>? Aliases = null,
    IReadOnlyDictionary<string, string>? Tags = null) : IIdentified;
