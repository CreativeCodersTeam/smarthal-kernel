using System.Text.Json.Nodes;

namespace SmartHal.Contracts.Integration.Transforms;

/// <summary>
/// Maps raw device values to core values through a table.
/// </summary>
/// <param name="Map">The core value for every raw value, keyed by the raw value in its text form.</param>
public sealed record EnumMapStep(IReadOnlyDictionary<string, JsonNode?> Map) : TransformStep;
