using SmartHal.Contracts.Primitives;

namespace SmartHal.Contracts.Topology;

/// <summary>
/// Describes a channel: a functional sub-unit of a device and an addressable target.
/// </summary>
/// <remarks>
/// <para>
/// A channel corresponds roughly to a Matter endpoint. Channel <c>0</c> is the root channel with the device-wide
/// functions such as device info, connectivity, identify, firmware, battery, alarms and, on gateways, bridge.
/// </para>
/// <para>
/// Equality compares list, dictionary and <see cref="System.Text.Json.Nodes.JsonNode"/> members by reference, not
/// by content; to compare contents, compare the JSON forms written with
/// <see cref="SmartHal.Contracts.Serialization.ContractsJson.Options"/>.
/// </para>
/// </remarks>
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
