using System.Text.Json.Nodes;

namespace SmartHal.Contracts.Integration.Virtual;

/// <summary>
/// Describes a command call on a member of a virtual device.
/// </summary>
/// <remarks>
/// Equality compares list, dictionary and <see cref="System.Text.Json.Nodes.JsonNode"/> members by reference, not
/// by content; to compare contents, compare the JSON forms written with
/// <see cref="SmartHal.Contracts.Serialization.ContractsJson.Options"/>.
/// </remarks>
/// <param name="Command">The name of the command to invoke on the member, for example <c>on</c>.</param>
/// <param name="Parameters">The parameters of the call; <see langword="null"/> when it takes none.</param>
public sealed record MemberCall(string Command, IReadOnlyDictionary<string, JsonNode?>? Parameters = null);
