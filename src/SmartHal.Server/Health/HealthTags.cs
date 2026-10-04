namespace SmartHal.Server.Health;

/// <summary>
/// Defines the tags that classify health checks into liveness and readiness checks.
/// </summary>
public static class HealthTags
{
    /// <summary>The tag of the checks that answer whether the process is alive.</summary>
    public const string Live = "live";

    /// <summary>The tag of the checks that answer whether the process is ready.</summary>
    public const string Ready = "ready";
}
