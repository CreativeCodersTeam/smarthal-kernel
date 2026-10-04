using SmartHal.Contracts.Primitives;

namespace SmartHal.Contracts.Schema;

/// <summary>
/// Defines a kind of channel through its mandatory and optional capabilities, for example DimmableLight =
/// OnOff + Level.
/// </summary>
/// <param name="Name">The namespaced name of the profile, for example <c>core.profile.dimmablelight</c>.</param>
/// <param name="Version">The version of the profile.</param>
/// <param name="Capabilities">The capabilities the profile expects.</param>
public sealed record ChannelProfile(
    string Name,
    TypeVersion Version,
    IReadOnlyList<ProfileCapability> Capabilities);
