using System.Text.Json.Nodes;
using SmartHal.Contracts.Addressing;

namespace SmartHal.Contracts.Runtime;

/// <summary>
/// Describes one invocation of a command and its lifecycle.
/// </summary>
/// <param name="Id">The immutable id of the invocation; it serves as the correlation id.</param>
/// <param name="IdempotencyKey">The key the client supplied to deduplicate requests.</param>
/// <param name="Address">The address of the command.</param>
/// <param name="Parameters">The parameters of the call, keyed by name.</param>
/// <param name="Issuer">Who issued the command.</param>
/// <param name="Status">The current lifecycle stage.</param>
/// <param name="CreatedAt">The time the invocation was created.</param>
/// <param name="Deadline">The time the invocation times out.</param>
/// <param name="Transitions">The stages reached so far, in order.</param>
/// <param name="ParentId">The id of the parent invocation of a fan-out; <see langword="null"/> for a top-level invocation.</param>
/// <param name="Result">The result of a command with completion mode result; otherwise, <see langword="null"/>.</param>
/// <param name="Error">Why the invocation failed; <see langword="null"/> when it did not fail.</param>
public sealed record CommandInvocation(
    Guid Id,
    string IdempotencyKey,
    Address Address,
    IReadOnlyDictionary<string, JsonNode?> Parameters,
    Issuer Issuer,
    CommandStatus Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset Deadline,
    IReadOnlyList<StatusTransition> Transitions,
    Guid? ParentId = null,
    JsonNode? Result = null,
    CommandError? Error = null);
