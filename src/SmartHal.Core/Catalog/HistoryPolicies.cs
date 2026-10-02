using SmartHal.Contracts.Schema;

namespace SmartHal.Core.Catalog;

/// <summary>
/// Provides the three history policies the core catalog reuses.
/// </summary>
/// <remarks>
/// Numeric state properties use <see cref="Measurement"/>, Booleans and enums <see cref="State"/>, numeric diagnostic
/// properties <see cref="Diagnostic"/>. Strings and timestamps are not historized. Every access builds a new policy,
/// so no two capability types share one.
/// </remarks>
internal static class HistoryPolicies
{
    /// <summary>
    /// Gets the four aggregates every rollup of the core catalog stores, as a new read-only list on every access.
    /// </summary>
    private static IReadOnlyList<RollupAggregate> AllAggregates =>
        Array.AsReadOnly([RollupAggregate.Min, RollupAggregate.Max, RollupAggregate.Avg, RollupAggregate.Last]);

    /// <summary>
    /// Gets the policy for Booleans and enums: raw values for 365 days, no compaction, no deadband.
    /// </summary>
    public static HistoryPolicy State => new HistoryPolicy(TimeSpan.FromDays(365));

    /// <summary>
    /// Gets the policy for numeric diagnostic values: raw values for 30 days, hourly compaction for 365 days.
    /// </summary>
    public static HistoryPolicy Diagnostic =>
        new HistoryPolicy(TimeSpan.FromDays(30), [new Rollup(TimeSpan.FromHours(1), AllAggregates, TimeSpan.FromDays(365))]);

    /// <summary>
    /// Creates the policy for numeric state values: raw values for 30 days, one-minute compaction for 90 days,
    /// hourly compaction for five years, and a minimum interval of 10 seconds between stored values.
    /// </summary>
    /// <param name="absoluteDeadband">The minimum absolute change in the unit of the property; <see langword="null"/> when every change is
    /// stored.</param>
    /// <returns>The measurement policy with the given deadband.</returns>
    public static HistoryPolicy Measurement(double? absoluteDeadband = null) =>
        new HistoryPolicy(TimeSpan.FromDays(30),
        [
            new Rollup(TimeSpan.FromMinutes(1), AllAggregates, TimeSpan.FromDays(90)),
            new Rollup(TimeSpan.FromHours(1), AllAggregates, TimeSpan.FromDays(5 * 365))
        ], new Deadband(Absolute: absoluteDeadband, MinInterval: TimeSpan.FromSeconds(10)));
}
