namespace SmartHal.Server.Composition;

/// <summary>
/// Defines the exit codes of the server process.
/// </summary>
public static class ExitCodes
{
    /// <summary>The process shut down in an orderly fashion.</summary>
    public const int Success = 0;

    /// <summary>An unhandled error ended the process.</summary>
    public const int UnhandledError = 1;

    /// <summary>The configuration is invalid and the start was aborted.</summary>
    public const int InvalidConfiguration = 2;
}
