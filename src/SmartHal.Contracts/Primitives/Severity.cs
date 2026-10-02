namespace SmartHal.Contracts.Primitives;

/// <summary>
/// Specifies how severe an event or an alarm is.
/// </summary>
public enum Severity
{
    /// <summary>A purely informational occurrence.</summary>
    Info = 0,

    /// <summary>An occurrence that deserves attention but needs no immediate action.</summary>
    Warning = 1,

    /// <summary>A fault of limited impact.</summary>
    Minor = 2,

    /// <summary>A fault that impairs operation and needs prompt action.</summary>
    Major = 3,

    /// <summary>A fault that stops operation or endangers people or equipment.</summary>
    Critical = 4
}
