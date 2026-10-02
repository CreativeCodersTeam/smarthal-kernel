namespace SmartHal.Contracts.Integration;

/// <summary>
/// Specifies the operating state of an adapter.
/// </summary>
public enum AdapterStatus
{
    /// <summary>The adapter runs and serves its connections.</summary>
    Running = 0,

    /// <summary>The adapter has been stopped.</summary>
    Stopped = 1,

    /// <summary>The adapter has failed.</summary>
    Error = 2
}
