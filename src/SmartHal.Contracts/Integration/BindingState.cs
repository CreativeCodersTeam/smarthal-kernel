namespace SmartHal.Contracts.Integration;

/// <summary>
/// Specifies whether a binding reaches its device.
/// </summary>
public enum BindingState
{
    /// <summary>The binding reaches its device.</summary>
    Online = 0,

    /// <summary>The binding does not reach its device; the capabilities it serves get the quality <c>uncertain</c>.</summary>
    Offline = 1,

    /// <summary>The binding has failed.</summary>
    Error = 2
}
