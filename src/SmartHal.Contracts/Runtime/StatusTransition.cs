namespace SmartHal.Contracts.Runtime;

/// <summary>
/// Records when a command invocation reached a lifecycle stage.
/// </summary>
/// <param name="Status">The stage reached.</param>
/// <param name="At">The time the stage was reached.</param>
public sealed record StatusTransition(CommandStatus Status, DateTimeOffset At);
