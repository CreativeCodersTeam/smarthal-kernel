using SmartHal.Contracts.Runtime;

namespace SmartHal.Contracts.Api;

/// <summary>
/// Delivers the current state of everything a subscription selects.
/// </summary>
/// <param name="States">The current property states.</param>
/// <param name="Cursor">The cursor from which the following changes are delivered.</param>
public sealed record SnapshotItem(IReadOnlyList<PropertyState> States, string Cursor) : SubscriptionItem;
