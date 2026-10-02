namespace SmartHal.Contracts.Integration;

/// <summary>
/// Specifies whether a binding connects a device to a protocol adapter or computes it internally.
/// </summary>
public enum BindingKind
{
    /// <summary>The binding assigns a real device to a protocol adapter.</summary>
    Protocol = 0,

    /// <summary>The binding computes a virtual device from its members.</summary>
    Internal = 1
}
