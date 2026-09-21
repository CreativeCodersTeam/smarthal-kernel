using System.Runtime.InteropServices;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SmartHal.Server.Composition;
using SmartHal.Server.Diagnostics;

namespace SmartHal.Server.Hosting;

/// <summary>
/// Turns the shutdown signals of the process into the orderly shutdown of the host, and a repeated
/// interrupt into an immediate end (FR-15).
/// </summary>
/// <remarks>
/// <para>
/// The first signal - <c>SIGTERM</c> or <c>SIGINT</c> - writes event <c>1002</c> with the name of
/// the signal and asks the host to stop; the shutdown from there on is bounded by
/// <c>SmartHal:ShutdownTimeout</c> (FR-16). A second <c>SIGINT</c> is the request to stop waiting:
/// it writes event <c>1005</c> and ends the process at once with
/// <see cref="ExitCodes.UnhandledError"/>, the exit code the plan chose for the abort (G-7). A
/// repeated <c>SIGTERM</c> is ignored, because it carries no such meaning.
/// </para>
/// <para>
/// How the process actually ends is a constructor argument rather than a call to
/// <see cref="Environment.Exit(int)"/> in this type, so a test can drive the same rule in process
/// without ending the test run (C-4).
/// </para>
/// </remarks>
public sealed class ShutdownSignalHandler
{
    private readonly IHostApplicationLifetime _lifetime;

    private readonly ILogger<ShutdownSignalHandler> _logger;

    private readonly Action<int> _terminate;

    // 0 until the first signal has been handled; the exchange below makes the first one the winner
    // even when two signals arrive on two threads at the same time.
    private int _shutdownRequested;

    /// <summary>
    /// Initialises a new instance of the <see cref="ShutdownSignalHandler"/> class.
    /// </summary>
    /// <param name="lifetime">The lifetime the first signal asks to stop the application.</param>
    /// <param name="logger">The logger the signal events are written to.</param>
    /// <param name="terminate">
    /// Ends the process with the given exit code; <see cref="Environment.Exit(int)"/> in production.
    /// </param>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public ShutdownSignalHandler(
        IHostApplicationLifetime lifetime,
        ILogger<ShutdownSignalHandler> logger,
        Action<int> terminate)
    {
        ArgumentNullException.ThrowIfNull(lifetime);
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(terminate);

        _lifetime = lifetime;
        _logger = logger;
        _terminate = terminate;
    }

    /// <summary>
    /// Handles one shutdown signal.
    /// </summary>
    /// <param name="signal">The signal that arrived.</param>
    /// <remarks>
    /// The first call starts the orderly shutdown, whichever of the two signals it carries. Every
    /// further call is an abort when it carries <see cref="PosixSignal.SIGINT"/> and is ignored
    /// otherwise.
    /// </remarks>
    public void Handle(PosixSignal signal)
    {
        if (Interlocked.Exchange(ref _shutdownRequested, 1) == 0)
        {
            _logger.ShutdownRequested(signal);
            _lifetime.StopApplication();

            return;
        }

        if (signal != PosixSignal.SIGINT)
        {
            return;
        }

        _logger.ShutdownAborted(ExitCodes.UnhandledError);
        _terminate(ExitCodes.UnhandledError);
    }
}
