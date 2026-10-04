using System.Text.Json.Nodes;
using SmartHal.Contracts.Addressing;
using SmartHal.Contracts.Runtime;

namespace SmartHal.Contracts.Api;

/// <summary>
/// Requests the invocation of a command.
/// </summary>
/// <param name="Address">The address of the command, by UUIDs or by keys.</param>
/// <param name="Parameters">The parameters of the call, keyed by name.</param>
/// <param name="Issuer">Who issues the command.</param>
/// <param name="IdempotencyKey">The key that deduplicates repeated requests.</param>
public sealed record InvokeRequest(
    ElementRef Address,
    IReadOnlyDictionary<string, JsonNode?> Parameters,
    Issuer Issuer,
    string IdempotencyKey);
