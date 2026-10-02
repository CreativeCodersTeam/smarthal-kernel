using System.Text.Json.Nodes;
using SmartHal.Contracts.Addressing;

namespace SmartHal.Contracts.Runtime;

/// <summary>
/// Describes one invocation of a command and its lifecycle.
/// </summary>
/// <remarks>
/// <para>
/// A command to a virtual device creates a parent invocation and one child invocation per member, linked through
/// <see cref="ParentId"/>. The parent is completed when every child succeeds, failed when none does, and partial
/// otherwise; skipped children do not count as failures.
/// </para>
/// <para>
/// While an invocation is open the UI may show an expected value; it is derived, never stored.
/// </para>
/// <para>
/// Equality compares list, dictionary and <see cref="System.Text.Json.Nodes.JsonNode"/> members by reference, not
/// by content; to compare contents, compare the JSON forms written with
/// <see cref="SmartHal.Contracts.Serialization.ContractsJson.Options"/>.
/// </para>
/// </remarks>
/// <param name="Id">The immutable id of the invocation; it serves as the correlation id.</param>
/// <param name="IdempotencyKey">The key supplied by the client; the same key within the time window returns the same invocation.</param>
/// <param name="Address">The address of the command.</param>
/// <param name="Parameters">The parameters of the call, keyed by name.</param>
/// <param name="Issuer">Who issued the command.</param>
/// <param name="Status">The current lifecycle stage.</param>
/// <param name="CreatedAt">The time the invocation was created.</param>
/// <param name="Deadline">The time the invocation times out: the command timeout, plus the wake-up interval for a sleepy device.</param>
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
