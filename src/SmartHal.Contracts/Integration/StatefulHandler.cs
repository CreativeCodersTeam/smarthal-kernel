using System.Text.Json.Nodes;

namespace SmartHal.Contracts.Integration;

/// <summary>
/// Names a stateful transform that combines a sequence of protocol events into one core event.
/// </summary>
/// <param name="Handler">The name of the handler.</param>
/// <param name="Config">The configuration of the handler; <see langword="null"/> when it needs none.</param>
public sealed record StatefulHandler(string Handler, IReadOnlyDictionary<string, JsonNode?>? Config = null);
