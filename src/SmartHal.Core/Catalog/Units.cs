namespace SmartHal.Core.Catalog;

/// <summary>
/// Provides the UCUM codes of the canonical units the core catalog uses.
/// </summary>
/// <remarks>
/// Every measured quantity has one canonical unit; bindings convert device values into it, and the display may choose
/// another one.
/// </remarks>
internal static class Units
{
    /// <summary>Percent.</summary>
    public const string Percent = "%";

    /// <summary>Degree Celsius.</summary>
    public const string Celsius = "Cel";

    /// <summary>Kelvin.</summary>
    public const string Kelvin = "K";

    /// <summary>Watt.</summary>
    public const string Watt = "W";

    /// <summary>Watt-hour.</summary>
    public const string WattHour = "W.h";

    /// <summary>Volt.</summary>
    public const string Volt = "V";

    /// <summary>Ampere.</summary>
    public const string Ampere = "A";

    /// <summary>Bar.</summary>
    public const string Bar = "bar";

    /// <summary>Cubic metre per hour.</summary>
    public const string CubicMetrePerHour = "m3/h";

    /// <summary>Cubic metre.</summary>
    public const string CubicMetre = "m3";

    /// <summary>Revolutions per minute.</summary>
    public const string PerMinute = "/min";

    /// <summary>Millimetre per second.</summary>
    public const string MillimetrePerSecond = "mm/s";

    /// <summary>Metre.</summary>
    public const string Metre = "m";

    /// <summary>Hour.</summary>
    public const string Hour = "h";

    /// <summary>Degree of arc.</summary>
    public const string Degree = "deg";
}
