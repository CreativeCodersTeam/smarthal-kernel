namespace SmartHal.Contracts.Schema;

/// <summary>
/// Specifies the role of a property.
/// </summary>
/// <remarks>
/// Every property is read-only; values change through commands only. For a <see cref="Config"/> property a
/// <c>set&lt;Property&gt;</c> command is derived automatically.
/// </remarks>
public enum PropertyCategory
{
    /// <summary>A state the device reports, for example whether a light is on.</summary>
    State = 0,

    /// <summary>A setting of the device; a setter command is derived for it.</summary>
    Config = 1,

    /// <summary>A diagnostic value, for example the link quality or a serial number.</summary>
    Diagnostic = 2
}
