namespace SmartHal.Contracts.Schema;

/// <summary>
/// Specifies when a command counts as completed.
/// </summary>
public enum Completion
{
    /// <summary>The device acknowledges the receipt.</summary>
    Ack = 0,

    /// <summary>A property named in <see cref="CommandDef.Affects"/> reaches its target value within the timeout.</summary>
    Confirmed = 1,

    /// <summary>The device returns a result that matches <see cref="CommandDef.Result"/>.</summary>
    Result = 2
}
