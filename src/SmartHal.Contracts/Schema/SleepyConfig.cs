namespace SmartHal.Contracts.Schema;

/// <summary>
/// Marks a device type as sleepy: its devices are reachable only when they wake up.
/// </summary>
/// <param name="WakeInterval">The interval in which the device wakes up.</param>
public sealed record SleepyConfig(TimeSpan WakeInterval);
