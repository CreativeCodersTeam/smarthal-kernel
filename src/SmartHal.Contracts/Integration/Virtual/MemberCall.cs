using System.Text.Json.Nodes;

namespace SmartHal.Contracts.Integration.Virtual;

/// <summary>
/// Describes a command call on a member of a virtual device.
/// </summary>
/// <param name="Command">The name of the command to invoke on the member, for example <c>on</c>.</param>
/// <param name="Parameters">The parameters of the call; <see langword="null"/> when it takes none.</param>
public sealed record MemberCall(string Command, IReadOnlyDictionary<string, JsonNode?>? Parameters = null);
