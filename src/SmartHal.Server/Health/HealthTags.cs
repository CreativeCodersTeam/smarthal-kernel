namespace SmartHal.Server.Health;

/// <summary>
/// The tags every health check of the server carries (FR-39).
/// </summary>
/// <remarks>
/// A check tagged <see cref="Live"/> answers whether the process is alive at all; a check tagged
/// <see cref="Ready"/> answers whether it can do its work. The readiness state of FR-40 is the
/// combined result of the <see cref="Ready"/> checks alone.
/// </remarks>
public static class HealthTags
{
    /// <summary>The tag of the checks that answer whether the process is alive.</summary>
    public const string Live = "live";

    /// <summary>The tag of the checks that answer whether the process is ready.</summary>
    public const string Ready = "ready";
}
