using System.Text.Json.Nodes;
using SmartHal.Contracts.Addressing;
using SmartHal.Contracts.Primitives;

namespace SmartHal.Contracts.Runtime;

/// <summary>
/// Describes one occurrence of an event; it never changes afterwards.
/// </summary>
/// <remarks>
/// <para>
/// Every state change of an alarm produces an event occurrence as well.
/// </para>
/// <para>
/// Equality compares list, dictionary and <see cref="System.Text.Json.Nodes.JsonNode"/> members by reference, not
/// by content; to compare contents, compare the JSON forms written with
/// <see cref="SmartHal.Contracts.Serialization.ContractsJson.Options"/>.
/// </para>
/// </remarks>
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
