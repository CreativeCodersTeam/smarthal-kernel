namespace SmartHal.Contracts.Api;

/// <summary>
/// Announces that the missed changes cannot be replayed and that a new snapshot follows.
/// </summary>
/// <param name="Reason">Why a resync is needed, for example <c>cursor_expired</c>.</param>
public sealed record ResyncItem(string Reason) : SubscriptionItem;
