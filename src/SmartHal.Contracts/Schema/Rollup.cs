namespace SmartHal.Contracts.Schema;

/// <summary>
/// Defines one compaction stage of a history policy, for example one minute for 90 days.
/// </summary>
/// <remarks>
/// Equality compares list, dictionary and <see cref="System.Text.Json.Nodes.JsonNode"/> members by reference, not
/// by content; to compare contents, compare the JSON forms written with
/// <see cref="SmartHal.Contracts.Serialization.ContractsJson.Options"/>.
/// </remarks>
/// <param name="Interval">The length of one compacted interval.</param>
/// <param name="Aggregates">The aggregates stored per interval.</param>
/// <param name="Retention">How long the compacted values are kept.</param>
public sealed record Rollup(TimeSpan Interval, IReadOnlyList<RollupAggregate> Aggregates, TimeSpan Retention);
