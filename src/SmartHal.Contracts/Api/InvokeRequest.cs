using System.Text.Json.Nodes;
using SmartHal.Contracts.Addressing;
using SmartHal.Contracts.Runtime;

namespace SmartHal.Contracts.Api;

/// <summary>
/// Requests the invocation of a command.
/// </summary>
/// <remarks>
/// <para>
/// The same <see cref="IdempotencyKey"/> within the time window returns the same invocation instead of a new one.
/// Alarms are acknowledged and shelved through this request as well, as commands of <c>core.alarms</c>.
/// </para>
/// <para>
/// Equality compares list, dictionary and <see cref="System.Text.Json.Nodes.JsonNode"/> members by reference, not
/// by content; to compare contents, compare the JSON forms written with
/// <see cref="SmartHal.Contracts.Serialization.ContractsJson.Options"/>.
/// </para>
/// </remarks>
/// <param name="Address">The address of the command, by UUIDs or by keys.</param>
/// <param name="Parameters">The parameters of the call, keyed by name.</param>
/// <param name="Issuer">Who issues the command.</param>
/// <param name="IdempotencyKey">The mandatory key that makes a repeated request return the same invocation.</param>
public sealed record InvokeRequest(
    ElementRef Address,
    IReadOnlyDictionary<string, JsonNode?> Parameters,
    Issuer Issuer,
    string IdempotencyKey);
