using System.Text.Json.Nodes;
using SmartHal.Contracts.Primitives;

namespace SmartHal.Contracts.Discovery;

/// <summary>
/// Describes a device an adapter has found and placed in the inbox.
/// </summary>
/// <param name="Id">The immutable id of the discovery result.</param>
/// <param name="AdapterId">The id of the adapter that found the device.</param>
/// <param name="Address">The protocol address of the device.</param>
/// <param name="Parameters">The binding parameters the adapter found, for example <c>ieeeAddr</c>.</param>
/// <param name="Status">What the user has decided about the result.</param>
/// <param name="DiscoveredAt">The time the adapter found the device.</param>
/// <param name="SuggestedType">The device type the adapter assumes; <see langword="null"/> when it cannot tell.</param>
public sealed record DiscoveryResult(
    Guid Id,
    Guid AdapterId,
    string Address,
    IReadOnlyDictionary<string, JsonNode?> Parameters,
    DiscoveryStatus Status,
    DateTimeOffset DiscoveredAt,
    TypeRef? SuggestedType = null);
