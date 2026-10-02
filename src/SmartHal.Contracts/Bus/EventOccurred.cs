using SmartHal.Contracts.Runtime;

namespace SmartHal.Contracts.Bus;

/// <summary>
/// Reports an event occurrence, including every state change of an alarm.
/// </summary>
/// <param name="Seq">The sequence number of the message within the event stream.</param>
/// <param name="Event">The occurrence.</param>
public sealed record EventOccurred(long Seq, EventOccurrence Event) : BusMessage(Seq);
