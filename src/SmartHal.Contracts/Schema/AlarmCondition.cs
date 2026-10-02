namespace SmartHal.Contracts.Schema;

/// <summary>
/// Specifies the condition a rule alarm evaluates on its property.
/// </summary>
public enum AlarmCondition
{
    /// <summary>The value is above the limit.</summary>
    Above = 0,

    /// <summary>The value is below the limit.</summary>
    Below = 1,

    /// <summary>The value equals the configured value.</summary>
    Equals = 2,

    /// <summary>The property has not been reported for longer than allowed.</summary>
    Stale = 3
}
