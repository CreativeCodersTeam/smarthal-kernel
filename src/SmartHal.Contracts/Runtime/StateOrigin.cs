namespace SmartHal.Contracts.Runtime;

/// <summary>
/// Specifies where a property state comes from.
/// </summary>
public enum StateOrigin
{
    /// <summary>A real device reported the state.</summary>
    Device = 0,

    /// <summary>The internal binding of a virtual device computed the state.</summary>
    Virtual = 1
}
