using System.Text.Json.Nodes;
using SmartHal.Contracts.Addressing;
using SmartHal.Contracts.Primitives;

namespace SmartHal.Contracts.Runtime;

/// <summary>
/// Describes one occurrence of an event.
/// </summary>
/// <param name="Id">The immutable id of the occurrence.</param>
/// <param name="Address">The address of the event, which names its source.</param>
/// <param name="OccurredAt">The time the event occurred.</param>
/// <param name="ReceivedTs">The time the platform received the event.</param>
/// <param name="Payload">The payload of the event; <see langword="null"/> when it carries none.</param>
/// <param name="Severity">The severity of the event; <see langword="null"/> when it has none.</param>
public sealed record EventOccurrence(
    Guid Id,
    Address Address,
    DateTimeOffset OccurredAt,
    DateTimeOffset ReceivedTs,
    JsonNode? Payload = null,
    Severity? Severity = null);
