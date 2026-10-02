namespace SmartHal.Contracts.Integration;

/// <summary>
/// Specifies the state of a connection of an adapter.
/// </summary>
public enum ConnectionStatus
{
    /// <summary>The connection is established.</summary>
    Connected = 0,

    /// <summary>The connection is not established.</summary>
    Disconnected = 1,

    /// <summary>The connection has failed.</summary>
    Error = 2
}
