namespace SmartHal.Contracts.Schema;

/// <summary>
/// Defines how the values of a property are kept over time.
/// </summary>
/// <remarks>
/// <para>
/// The property definition sets the default and every property instance may override it. Only scalars and enums are
/// historized directly; structs are split into their fields.
/// </para>
/// <para>
/// Equality compares list, dictionary and <see cref="System.Text.Json.Nodes.JsonNode"/> members by reference, not
/// by content; to compare contents, compare the JSON forms written with
/// <see cref="SmartHal.Contracts.Serialization.ContractsJson.Options"/>.
/// </para>
/// </remarks>
/// <param name="RawRetention">How long raw values are kept.</param>
/// <param name="Rollups">The compaction stages, each with its own interval, aggregates and retention; <see langword="null"/> when values
/// are not compacted.</param>
/// <param name="Deadband">The minimum change that is stored; <see langword="null"/> when every change is stored.</param>
public sealed record HistoryPolicy(
    TimeSpan RawRetention,
    IReadOnlyList<Rollup>? Rollups = null,
    Deadband? Deadband = null);
