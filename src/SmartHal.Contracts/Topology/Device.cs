using SmartHal.Contracts.Primitives;

namespace SmartHal.Contracts.Topology;

/// <summary>
/// Describes a device: its identity, its lifecycle and the container of its channels.
/// </summary>
/// <remarks>
/// <para>
/// Values never hang off the device directly but always off a capability. Every device has exactly one root channel
/// <c>0</c> with the device-wide functions.
/// </para>
/// <para>
/// A gateway is an ordinary device with <c>core.bridge</c> on its root channel; its sub-devices point to it through
/// <see cref="ConnectedVia"/>, over several stages if needed. A virtual device uses the same model with an internal
/// binding instead of a protocol binding.
/// </para>
/// <para>
/// Equality compares list, dictionary and <see cref="System.Text.Json.Nodes.JsonNode"/> members by reference, not
/// by content; to compare contents, compare the JSON forms written with
/// <see cref="SmartHal.Contracts.Serialization.ContractsJson.Options"/>.
/// </para>
/// </remarks>
/// <param name="Id">The immutable id of the device.</param>
/// <param name="Key">The globally unique key of the device, for example <c>halle2.pumpe3</c>.</param>
/// <param name="Name">The display name of the device.</param>
/// <param name="Virtual"><see langword="true"/> when the device is computed from member devices; otherwise, <see
/// langword="false"/>.</param>
/// <param name="Lifecycle">The lifecycle stage of the device.</param>
/// <param name="Channels">The channels of the device, always including the root channel <c>0</c>.</param>
/// <param name="TypeRef">The device type; <see langword="null"/> only for a virtual device without a type.</param>
/// <param name="LocationId">The id of the location the device is installed at; <see langword="null"/> when it is not placed.</param>
/// <param name="ConnectedVia">The id of the gateway device the device is reached through; <see langword="null"/> when it is reached
/// directly.</param>
/// <param name="Aliases">The former keys, valid for a limited time; <see langword="null"/> when there are none.</param>
/// <param name="Tags">Free key/value tags the kernel does not check; <see langword="null"/> when there are none.</param>
public sealed record Device(
    Guid Id,
    string Key,
    string Name,
    bool Virtual,
    DeviceLifecycle Lifecycle,
    IReadOnlyList<Channel> Channels,
    TypeRef? TypeRef = null,
    Guid? LocationId = null,
    Guid? ConnectedVia = null,
    IReadOnlyList<string>? Aliases = null,
    IReadOnlyDictionary<string, string>? Tags = null) : IIdentified;
