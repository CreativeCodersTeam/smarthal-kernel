using System.Text.Json.Nodes;
using SmartHal.Contracts.Runtime;

namespace SmartHal.Contracts.Api;

/// <summary>
/// Describes one point of a time series: a raw value or the aggregates of a rollup interval.
/// </summary>
/// <remarks>
/// Equality compares list, dictionary and <see cref="System.Text.Json.Nodes.JsonNode"/> members by reference, not
/// by content; to compare contents, compare the JSON forms written with
/// <see cref="SmartHal.Contracts.Serialization.ContractsJson.Options"/>.
/// </remarks>
/// <param name="Ts">The time of the raw value, or the start of the rollup interval.</param>
/// <param name="Quality">The quality of the value; for a rollup the worst quality within the interval.</param>
/// <param name="Value">The raw value; <see langword="null"/> for a rollup point.</param>
/// <param name="Min">The smallest value of the rollup interval; <see langword="null"/> for a raw point or when not stored.</param>
/// <param name="Max">The largest value of the rollup interval; <see langword="null"/> for a raw point or when not stored.</param>
/// <param name="Avg">The mean value of the rollup interval; <see langword="null"/> for a raw point or when not stored.</param>
/// <param name="Last">The last value of the rollup interval, which may also be an enum value; <see langword="null"/> for a raw point or
/// when not stored.</param>
public sealed record HistoryPoint(
    DateTimeOffset Ts,
    QualityLevel Quality,
    JsonNode? Value = null,
    double? Min = null,
    double? Max = null,
    double? Avg = null,
    JsonNode? Last = null);
