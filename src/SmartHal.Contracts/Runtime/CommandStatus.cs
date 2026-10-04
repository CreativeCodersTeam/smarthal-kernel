namespace SmartHal.Contracts.Runtime;

/// <summary>
/// Specifies the lifecycle stage of a command invocation.
/// </summary>
public enum CommandStatus
{
    /// <summary>The invocation waits to be sent, for example until a sleepy device wakes up.</summary>
    Pending = 0,

    /// <summary>The invocation was sent to the device.</summary>
    Sent = 1,

    /// <summary>The device acknowledged the receipt.</summary>
    Acked = 2,

    /// <summary>The invocation completed successfully.</summary>
    Completed = 3,

    /// <summary>A fan-out invocation succeeded for some members and failed for others.</summary>
    Partial = 4,

    /// <summary>The invocation failed.</summary>
    Failed = 5,

    /// <summary>The invocation did not complete within its deadline.</summary>
    Timeout = 6,

    /// <summary>The invocation was cancelled while it was pending.</summary>
    Cancelled = 7,

    /// <summary>A child invocation of a fan-out was skipped because the member has no matching command; it is not a failure.</summary>
    Skipped = 8
}
