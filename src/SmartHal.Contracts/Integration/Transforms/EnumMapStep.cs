using System.Text.Json.Nodes;

namespace SmartHal.Contracts.Integration.Transforms;

/// <summary>
/// Maps raw device values to core values through a table.
/// </summary>
/// <remarks>
/// <para>
/// The step is invertible only when the table is one-to-one.
/// </para>
/// <para>
/// Equality compares list, dictionary and <see cref="System.Text.Json.Nodes.JsonNode"/> members by reference, not
/// by content; to compare contents, compare the JSON forms written with
/// <see cref="SmartHal.Contracts.Serialization.ContractsJson.Options"/>.
/// </para>
/// </remarks>
/// <param name="Map">The core value for every raw value, keyed by the raw value in its text form.</param>
public sealed record EnumMapStep(IReadOnlyDictionary<string, JsonNode?> Map) : TransformStep;
