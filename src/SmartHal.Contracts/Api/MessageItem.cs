using SmartHal.Contracts.Bus;

namespace SmartHal.Contracts.Api;

/// <summary>
/// Delivers one bus message of a subscription.
/// </summary>
/// <param name="Message">The bus message.</param>
/// <param name="Cursor">The cursor after this message; a subscription resumed with it continues behind the message.</param>
public sealed record MessageItem(BusMessage Message, string Cursor) : SubscriptionItem;
