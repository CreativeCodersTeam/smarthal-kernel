using Microsoft.Extensions.DependencyInjection;

namespace SmartHal.IntegrationTests.Hosting;

/// <summary>
/// The overrides a test applies to the host that <see cref="ServerHostHarness"/> starts.
/// </summary>
public sealed class HarnessOptions
{
    /// <summary>
    /// Gets the configuration values that win over every other source.
    /// </summary>
    /// <value>
    /// An in-memory source that the harness appends as the seventh and last source, behind the
    /// command line, so an override always wins.
    /// </value>
    public Dictionary<string, string?> Configuration { get; } = new(StringComparer.Ordinal);

    /// <summary>
    /// Gets or sets the host environment the process is started in.
    /// </summary>
    /// <value>The environment name, passed as <c>--environment</c>. The default is <c>Production</c>.</value>
    public string EnvironmentName { get; set; } = "Production";

    /// <summary>
    /// Gets the command line arguments the host is started with.
    /// </summary>
    /// <value>The arguments in front of the <c>--environment</c> pair the harness appends.</value>
    public List<string> Args { get; } = [];

    /// <summary>
    /// Gets the environment variables that are set for the duration of the harness.
    /// </summary>
    /// <value>
    /// A map of variable name to value; a <see langword="null"/> value removes the variable. The
    /// harness sets them process wide and restores the previous values when it is disposed, so tests
    /// using it belong in the <c>ProcessEnvironment</c> collection.
    /// </value>
    public Dictionary<string, string?> EnvironmentVariables { get; } = new(StringComparer.Ordinal);

    /// <summary>
    /// Gets or sets a callback that registers additional services.
    /// </summary>
    /// <value>The callback, or <see langword="null"/> when the test registers nothing.</value>
    public Action<IServiceCollection>? ConfigureServices { get; set; }

    /// <summary>
    /// Gets the recorder that stands in for ending the process.
    /// </summary>
    /// <value>
    /// The recorder the harness hands to <c>ShutdownSignalHandler</c> in place of
    /// <see cref="Environment.Exit(int)"/>. A second <c>SIGINT</c> therefore records its exit code
    /// here instead of ending the test run (C-4).
    /// </value>
    public TerminateRecorder TerminateRecorder { get; } = new();

    /// <summary>
    /// Gets or sets a value indicating whether the harness supplies a temporary data directory.
    /// </summary>
    /// <value>
    /// <see langword="true"/> - the default - lets the harness set <c>SmartHal:InstanceName</c> to
    /// <c>test</c> and <c>SmartHal:DataDirectory</c> to a folder below
    /// <see cref="ServerHostHarness.TempDirectory"/>, so a test without its own configuration starts
    /// with a valid one. A key that this test already put into <see cref="Configuration"/> keeps its
    /// value, so a test proving an invalid configuration sets it to <see langword="null"/> there.
    /// Set this property to <see langword="false"/> when the test supplies both values through a
    /// lower configuration source instead.
    /// </value>
    public bool UseTemporaryDataDirectory { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether the harness redirects Serilog's rolling file into its
    /// own temporary directory.
    /// </summary>
    /// <value>
    /// <see langword="true"/> - the default - overrides <c>Serilog:WriteTo:File:Args:path</c> with a
    /// file below <see cref="ServerHostHarness.TempDirectory"/> and its formatter with the compact
    /// JSON formatter, so <see cref="ServerHostHarness.ReadSerilogJsonLog"/> can read back what
    /// Serilog really wrote (C-6). Set it to <see langword="false"/> to leave the shipped file sink
    /// untouched.
    /// </value>
    public bool CaptureSerilogJson { get; set; } = true;
}

/// <summary>
/// Records the exit codes the shutdown signal handler would have ended the process with.
/// </summary>
/// <remarks>
/// A second <c>SIGINT</c> aborts the orderly shutdown and ends the process at once (FR-15, G-7).
/// Inside a test that call must not reach <see cref="Environment.Exit(int)"/>, because it would end
/// the test run; the harness hands this recorder to the handler instead, and the test reads the exit
/// code from <see cref="ExitCodes"/>.
/// </remarks>
public sealed class TerminateRecorder
{
    private readonly List<int> _exitCodes = [];

    /// <summary>
    /// Gets the exit codes that were recorded, in order.
    /// </summary>
    /// <value>An empty list until a signal asked to end the process.</value>
    public IReadOnlyList<int> ExitCodes
    {
        get
        {
            lock (_exitCodes)
            {
                return [.. _exitCodes];
            }
        }
    }

    /// <summary>
    /// Records one exit code in place of ending the process.
    /// </summary>
    /// <param name="exitCode">The exit code the process would have ended with.</param>
    public void Record(int exitCode)
    {
        lock (_exitCodes)
        {
            _exitCodes.Add(exitCode);
        }
    }
}
