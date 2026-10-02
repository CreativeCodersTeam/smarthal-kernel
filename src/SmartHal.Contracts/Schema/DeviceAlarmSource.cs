namespace SmartHal.Contracts.Schema;

/// <summary>
/// Describes an alarm the device reports through one of the events of the capability.
/// </summary>
/// <param name="Event">The name of the event that reports the alarm.</param>
public sealed record DeviceAlarmSource(string Event) : AlarmSource;
