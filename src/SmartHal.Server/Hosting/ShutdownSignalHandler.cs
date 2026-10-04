using System.Runtime.InteropServices;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SmartHal.Server.Composition;
using SmartHal.Server.Diagnostics;

namespace SmartHal.Server.Hosting;

/// <summary>
/// Maps the shutdown signals of the process to an orderly shutdown, or to an immediate end on a second interrupt.
/// </summary>
public sealed class ShutdownSignalHandler
{
    private readonly IHostApplicationLifetime _lifetime;

    private readonly ILogger<ShutdownSignalHandler> _logger;

    private readonly Action<int> _terminate;

    // 0 until the first signal has been handled; the exchange below makes the first one the winner
    // even when two signals arrive on two threads at the same time.
    private int _shutdownRequested;

    /// <summary>
    /// Initializes a new instance of the <see cref="ShutdownSignalHandler"/> class.
    /// </summary>
    /// <param name="lifetime">The lifetime used to stop the application.</param>
    /// <param name="logger">The logger the signal events are written to.</param>
    /// <param name="terminate">An action that ends the process with an exit code, replaceable in tests.</param>
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
    /// Handles a shutdown signal of the process.
    /// </summary>
    /// <param name="signal">The signal that arrived.</param>
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
