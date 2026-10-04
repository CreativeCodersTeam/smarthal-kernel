using SmartHal.Contracts.Bus;

namespace SmartHal.Contracts.Api;

/// <summary>
/// Delivers one bus message of a subscription.
/// </summary>
/// <param name="Message">The bus message.</param>
/// <param name="Cursor">The cursor to resume the subscription after this message.</param>
public sealed record MessageItem(BusMessage Message, string Cursor) : SubscriptionItem;
