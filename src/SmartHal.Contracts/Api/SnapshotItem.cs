using SmartHal.Contracts.Runtime;

namespace SmartHal.Contracts.Api;

/// <summary>
/// Delivers the current state of everything a subscription selects.
/// </summary>
/// <remarks>
/// Equality compares list, dictionary and <see cref="System.Text.Json.Nodes.JsonNode"/> members by reference, not
/// by content; to compare contents, compare the JSON forms written with
/// <see cref="SmartHal.Contracts.Serialization.ContractsJson.Options"/>.
/// </remarks>
/// <param name="States">The current property states.</param>
/// <param name="Cursor">The cursor from which the following changes are delivered.</param>
public sealed record SnapshotItem(IReadOnlyList<PropertyState> States, string Cursor) : SubscriptionItem;
