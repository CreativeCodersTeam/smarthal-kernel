namespace SmartHal.Server.Composition;

/// <summary>
/// The exit codes the server process returns (IF-5, FR-17).
/// </summary>
public static class ExitCodes
{
    /// <summary>The process shut down in an orderly fashion.</summary>
    public const int Success = 0;

    /// <summary>An unhandled error occurred during start-up or at run time.</summary>
    public const int UnhandledError = 1;

    /// <summary>The configuration is invalid and the start was aborted.</summary>
    public const int InvalidConfiguration = 2;
}
