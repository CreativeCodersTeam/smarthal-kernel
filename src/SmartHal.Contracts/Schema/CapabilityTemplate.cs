using SmartHal.Contracts.Primitives;

namespace SmartHal.Contracts.Schema;

/// <summary>
/// Describes a capability a device type creates on one of its channels.
/// </summary>
/// <param name="Key">The key of the capability within the channel, for example <c>druckseite</c>.</param>
/// <param name="Type">The capability type.</param>
/// <param name="Features">The feature flags the capability activates; <see langword="null"/> when it activates none.</param>
public sealed record CapabilityTemplate(string Key, TypeRef Type, IReadOnlyList<string>? Features = null);
