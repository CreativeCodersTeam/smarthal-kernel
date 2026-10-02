namespace SmartHal.Contracts.Discovery;

/// <summary>
/// Specifies what the user has decided about a discovery result in the inbox.
/// </summary>
public enum DiscoveryStatus
{
    /// <summary>The result waits for a decision.</summary>
    New = 0,

    /// <summary>The user approved the result; a device in stage provisioned was created from it.</summary>
    Approved = 1,

    /// <summary>The user rejected the result.</summary>
    Rejected = 2,

    /// <summary>The user ignores the result permanently.</summary>
    Ignored = 3
}
