namespace SmartHal.Contracts.Schema;

/// <summary>
/// Defines how the values of a property are kept over time.
/// </summary>
/// <param name="RawRetention">How long raw values are kept.</param>
/// <param name="Rollups">The compaction stages; <see langword="null"/> when values are not compacted.</param>
/// <param name="Deadband">The minimum change that is stored; <see langword="null"/> when every change is stored.</param>
public sealed record HistoryPolicy(
    TimeSpan RawRetention,
    IReadOnlyList<Rollup>? Rollups = null,
    Deadband? Deadband = null);
