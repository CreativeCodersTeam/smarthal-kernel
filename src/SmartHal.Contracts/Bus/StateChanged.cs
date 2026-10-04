using System.Text.Json.Nodes;
using SmartHal.Contracts.Runtime;

namespace SmartHal.Contracts.Bus;

/// <summary>
/// Reports a new property state, including a change of the quality alone.
/// </summary>
/// <param name="Seq">The sequence number of the message within the property.</param>
/// <param name="State">The new state.</param>
/// <param name="Previous">The previous value; <see langword="null"/> when there was none or it was <see langword="null"/>.</param>
/// <param name="PreviousQuality">The previous quality; <see langword="null"/> when there was no previous state.</param>
public sealed record StateChanged(long Seq, PropertyState State, JsonNode? Previous = null, Quality? PreviousQuality = null)
    : BusMessage(Seq);
