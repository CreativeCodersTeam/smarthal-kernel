using System.Text.Json.Serialization;

namespace SmartHal.Contracts.Api;

/// <summary>
/// Describes one item of a subscription stream.
/// </summary>
/// <remarks>
/// A subscription first delivers a snapshot of the current state, then the changes; every item carries a cursor.
/// With a cursor the kernel replays what was missed while it is still buffered; otherwise a resync item and a new
/// snapshot follow. The JSON form carries the discriminator <c>kind</c>: <c>snapshot</c>, <c>message</c> or
/// <c>resync</c>.
/// </remarks>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "kind")]
[JsonDerivedType(typeof(SnapshotItem), "snapshot")]
[JsonDerivedType(typeof(MessageItem), "message")]
[JsonDerivedType(typeof(ResyncItem), "resync")]
public abstract record SubscriptionItem;
