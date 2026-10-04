using System.Text.Json.Serialization;

namespace SmartHal.Contracts.Api;

/// <summary>
/// Describes one item of a subscription stream.
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "kind")]
[JsonDerivedType(typeof(SnapshotItem), "snapshot")]
[JsonDerivedType(typeof(MessageItem), "message")]
[JsonDerivedType(typeof(ResyncItem), "resync")]
public abstract record SubscriptionItem;
