namespace SmartHal.Contracts.Primitives;

/// <summary>
/// Specifies how a virtual device combines the values of a property across its members.
/// </summary>
public enum Aggregation
{
    /// <summary><see langword="true"/> as soon as one member is <see langword="true"/>.</summary>
    Any = 0,

    /// <summary><see langword="true"/> only when every member is <see langword="true"/>.</summary>
    All = 1,

    /// <summary>The arithmetic mean of the member values.</summary>
    Avg = 2,

    /// <summary>The smallest member value.</summary>
    Min = 3,

    /// <summary>The largest member value.</summary>
    Max = 4,

    /// <summary>The sum of the member values.</summary>
    Sum = 5,

    /// <summary>The most recently reported member value.</summary>
    Last = 6
}
