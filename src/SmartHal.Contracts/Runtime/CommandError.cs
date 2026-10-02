namespace SmartHal.Contracts.Runtime;

/// <summary>
/// Describes why a command invocation failed.
/// </summary>
/// <param name="Code">A machine-readable error code.</param>
/// <param name="Message">A human-readable description of the error.</param>
public sealed record CommandError(string Code, string Message);
