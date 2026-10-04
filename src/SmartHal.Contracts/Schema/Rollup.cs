namespace SmartHal.Contracts.Schema;

/// <summary>
/// Defines one compaction stage of a history policy, for example one minute for 90 days.
/// </summary>
/// <param name="Interval">The length of one compacted interval.</param>
/// <param name="Aggregates">The aggregates stored per interval.</param>
/// <param name="Retention">How long the compacted values are kept.</param>
public sealed record Rollup(TimeSpan Interval, IReadOnlyList<RollupAggregate> Aggregates, TimeSpan Retention);
