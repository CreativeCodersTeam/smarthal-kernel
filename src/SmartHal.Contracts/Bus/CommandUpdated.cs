using SmartHal.Contracts.Runtime;

namespace SmartHal.Contracts.Bus;

/// <summary>
/// Reports a status change of a command invocation.
/// </summary>
/// <param name="Seq">The sequence number of the message within the invocation.</param>
/// <param name="Invocation">The invocation in its new status.</param>
public sealed record CommandUpdated(long Seq, CommandInvocation Invocation) : BusMessage(Seq);
