using System.Text.Json.Nodes;
using SmartHal.Contracts.Runtime;

namespace SmartHal.Contracts.Api;

/// <summary>
/// Describes one point of a time series: a raw value or the aggregates of a rollup interval.
/// </summary>
/// <param name="Ts">The time of the raw value, or the start of the rollup interval.</param>
/// <param name="Quality">The quality of the value; for a rollup the worst quality within the interval.</param>
/// <param name="Value">The raw value; <see langword="null"/> for a rollup point.</param>
/// <param name="Min">The smallest value of the rollup interval; <see langword="null"/> for a raw point or when not stored.</param>
/// <param name="Max">The largest value of the rollup interval; <see langword="null"/> for a raw point or when not stored.</param>
/// <param name="Avg">The mean value of the rollup interval; <see langword="null"/> for a raw point or when not stored.</param>
/// <param name="Last">The last value of the rollup interval; <see langword="null"/> for a raw point or when not stored.</param>
public sealed record HistoryPoint(
    DateTimeOffset Ts,
    QualityLevel Quality,
    JsonNode? Value = null,
    double? Min = null,
    double? Max = null,
    double? Avg = null,
    JsonNode? Last = null);
