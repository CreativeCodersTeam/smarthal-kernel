using System.Text.Json.Nodes;

namespace SmartHal.Contracts.Integration;

/// <summary>
/// Names a stateful transform that combines a sequence of protocol events into one core event.
/// </summary>
/// <remarks>
/// <para>
/// For example, InitialPress, ShortRelease and MultiPressComplete become <c>pressed(double)</c>. The kernel only sees
/// the result.
/// </para>
/// <para>
/// Equality compares list, dictionary and <see cref="System.Text.Json.Nodes.JsonNode"/> members by reference, not
/// by content; to compare contents, compare the JSON forms written with
/// <see cref="SmartHal.Contracts.Serialization.ContractsJson.Options"/>.
/// </para>
/// </remarks>
/// <param name="Handler">The name of the handler.</param>
/// <param name="Config">The configuration of the handler; <see langword="null"/> when it needs none.</param>
public sealed record StatefulHandler(string Handler, IReadOnlyDictionary<string, JsonNode?>? Config = null);
