using System.Text.Json.Nodes;
using SmartHal.Contracts.Runtime;

namespace SmartHal.Contracts.Bus;

/// <summary>
/// Reports a new property state, including a change of the quality alone.
/// </summary>
/// <remarks>
/// <para>
/// The message is sent only when the value or the quality changed; an unchanged report only renews the receive time.
/// </para>
/// <para>
/// Equality compares list, dictionary and <see cref="System.Text.Json.Nodes.JsonNode"/> members by reference, not
/// by content; to compare contents, compare the JSON forms written with
/// <see cref="SmartHal.Contracts.Serialization.ContractsJson.Options"/>.
/// </para>
/// </remarks>
/// <param name="Seq">The sequence number of the message within the property.</param>
/// <param name="State">The new state.</param>
/// <param name="Previous">The previous value; <see langword="null"/> when there was none or it was <see langword="null"/>.</param>
/// <param name="PreviousQuality">The previous quality; <see langword="null"/> when there was no previous state.</param>
public sealed record StateChanged(long Seq, PropertyState State, JsonNode? Previous = null, Quality? PreviousQuality = null)
    : BusMessage(Seq);
