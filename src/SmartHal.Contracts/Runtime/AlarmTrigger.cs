namespace SmartHal.Contracts.Runtime;

/// <summary>
/// Specifies what raised an alarm instance.
/// </summary>
public enum AlarmTrigger
{
    /// <summary>The device reported the alarm.</summary>
    Device = 0,

    /// <summary>A platform rule raised the alarm.</summary>
    Rule = 1
}
