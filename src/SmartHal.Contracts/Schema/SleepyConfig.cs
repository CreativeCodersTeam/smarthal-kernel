namespace SmartHal.Contracts.Schema;

/// <summary>
/// Marks a device type as sleepy: its devices are reachable only when they wake up.
/// </summary>
/// <remarks>
/// Commands to a sleepy device stay pending until it wakes up; their effective timeout is the command timeout plus
/// <see cref="WakeInterval"/>.
/// </remarks>
/// <param name="WakeInterval">The interval in which the device wakes up.</param>
public sealed record SleepyConfig(TimeSpan WakeInterval);
