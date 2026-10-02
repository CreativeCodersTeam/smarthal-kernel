namespace SmartHal.Contracts.Integration;

/// <summary>
/// Specifies whether a binding reaches its device.
/// </summary>
/// <remarks>
/// <c>core.connectivity</c> derives the device status from the states of all bindings of the device:
/// <c>online</c> when all are online, <c>degraded</c> when some are, <c>offline</c> when none is.
/// </remarks>
public enum BindingState
{
    /// <summary>The binding reaches its device.</summary>
    Online = 0,

    /// <summary>The binding does not reach its device; the capabilities it serves get the quality <c>uncertain</c>.</summary>
    Offline = 1,

    /// <summary>The binding has failed.</summary>
    Error = 2
}
