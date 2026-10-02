using SmartHal.Contracts.Primitives;

namespace SmartHal.Contracts.Schema;

/// <summary>
/// Defines a kind of channel through its mandatory and optional capabilities, for example DimmableLight =
/// OnOff + Level.
/// </summary>
/// <remarks>
/// Equality compares list, dictionary and <see cref="System.Text.Json.Nodes.JsonNode"/> members by reference, not
/// by content; to compare contents, compare the JSON forms written with
/// <see cref="SmartHal.Contracts.Serialization.ContractsJson.Options"/>.
/// </remarks>
/// <param name="Name">The namespaced name of the profile, for example <c>core.profile.dimmablelight</c>.</param>
/// <param name="Version">The version of the profile.</param>
/// <param name="Capabilities">The capabilities the profile expects.</param>
public sealed record ChannelProfile(
    string Name,
    TypeVersion Version,
    IReadOnlyList<ProfileCapability> Capabilities);
