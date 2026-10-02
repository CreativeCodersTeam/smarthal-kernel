namespace SmartHal.Contracts.Runtime;

/// <summary>
/// Specifies how trustworthy a value is.
/// </summary>
public enum QualityLevel
{
    /// <summary>The value is trustworthy.</summary>
    Good = 0,

    /// <summary>The value may be outdated or inaccurate, for example because its source is offline.</summary>
    Uncertain = 1,

    /// <summary>The value is not usable.</summary>
    Bad = 2
}
