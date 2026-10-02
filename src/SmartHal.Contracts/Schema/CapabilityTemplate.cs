using SmartHal.Contracts.Primitives;

namespace SmartHal.Contracts.Schema;

/// <summary>
/// Describes a capability a device type creates on one of its channels.
/// </summary>
/// <remarks>
/// Equality compares list, dictionary and <see cref="System.Text.Json.Nodes.JsonNode"/> members by reference, not
/// by content; to compare contents, compare the JSON forms written with
/// <see cref="SmartHal.Contracts.Serialization.ContractsJson.Options"/>.
/// </remarks>
/// <param name="Key">The key of the capability within the channel, for example <c>druckseite</c>.</param>
/// <param name="Type">The capability type.</param>
/// <param name="Features">The feature flags the capability activates; <see langword="null"/> when it activates none.</param>
public sealed record CapabilityTemplate(string Key, TypeRef Type, IReadOnlyList<string>? Features = null);
