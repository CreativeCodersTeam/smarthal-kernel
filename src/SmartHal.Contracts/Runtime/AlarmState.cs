namespace SmartHal.Contracts.Runtime;

/// <summary>
/// Specifies the state of an alarm instance after ISA-18.2.
/// </summary>
/// <remarks>
/// An alarm moves from active/unacked either to active/acked and then to cleared, or to cleared/unacked and then to
/// cleared.
/// </remarks>
public enum AlarmState
{
    /// <summary>The condition is present and nobody has acknowledged the alarm.</summary>
    ActiveUnacked = 0,

    /// <summary>The condition is present and the alarm has been acknowledged.</summary>
    ActiveAcked = 1,

    /// <summary>The condition has gone but nobody has acknowledged the alarm.</summary>
    ClearedUnacked = 2,

    /// <summary>The condition has gone and the alarm is closed.</summary>
    Cleared = 3
}
