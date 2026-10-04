using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using SmartHal.Contracts.Addressing;

namespace SmartHal.Contracts.Runtime;

/// <summary>
/// Describes the latest reported value of a property.
/// </summary>
/// <param name="Address">The address of the property.</param>
/// <param name="Value">The reported value; <see langword="null"/> only when the quality is bad.</param>
/// <param name="ReceivedTs">The time the platform received the value.</param>
/// <param name="Quality">The quality of the value.</param>
/// <param name="Origin">Where the value comes from.</param>
/// <param name="Seq">The sequence number of the value within its property.</param>
/// <param name="SourceTs">The time the device took the value, when the device reports it; otherwise, <see langword="null"/>.</param>
public sealed record PropertyState(
    Address Address,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)] JsonNode? Value,
    DateTimeOffset ReceivedTs,
    Quality Quality,
    StateOrigin Origin,
    long Seq,
    DateTimeOffset? SourceTs = null);
