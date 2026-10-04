namespace SmartHal.Contracts.Runtime;

/// <summary>
/// Provides the known reasons for a quality other than good.
/// </summary>
public static class QualityReasons
{
    /// <summary>A gateway on the way to the device is offline.</summary>
    public const string UpstreamOffline = "upstream_offline";

    /// <summary>The binding that serves the capability is offline.</summary>
    public const string SourceOffline = "source_offline";

    /// <summary>The device reported a special value that means "no value".</summary>
    public const string InvalidValue = "invalid_value";

    /// <summary>The value has not been reported for too long.</summary>
    public const string Stale = "stale";

    /// <summary>A member of a virtual device has the quality bad.</summary>
    public const string MemberBad = "member_bad";
}
