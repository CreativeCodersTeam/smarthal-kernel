namespace SmartHal.Contracts.Schema;

/// <summary>
/// Specifies an aggregate that a history rollup stores per interval.
/// </summary>
public enum RollupAggregate
{
    /// <summary>The smallest value of the interval.</summary>
    Min = 0,

    /// <summary>The largest value of the interval.</summary>
    Max = 1,

    /// <summary>The arithmetic mean of the interval.</summary>
    Avg = 2,

    /// <summary>The last value of the interval.</summary>
    Last = 3
}
