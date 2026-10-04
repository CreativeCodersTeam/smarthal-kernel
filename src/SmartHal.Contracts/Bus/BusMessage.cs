using System.Text.Json.Serialization;

namespace SmartHal.Contracts.Bus;

/// <summary>
/// Describes a message on the bus that distributes every change.
/// </summary>
/// <param name="Seq">The sequence number within the source, for detecting duplicates and gaps.</param>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "kind")]
[JsonDerivedType(typeof(StateChanged), "StateChanged")]
[JsonDerivedType(typeof(EventOccurred), "EventOccurred")]
[JsonDerivedType(typeof(CommandUpdated), "CommandUpdated")]
public abstract record BusMessage(long Seq);
