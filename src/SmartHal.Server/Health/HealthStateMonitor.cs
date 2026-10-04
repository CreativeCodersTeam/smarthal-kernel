using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using SmartHal.Server.Diagnostics;

namespace SmartHal.Server.Health;

/// <summary>
/// Makes the readiness state observable from outside the process by logging every change.
/// </summary>
public sealed class HealthStateMonitor : IHealthCheckPublisher
{
    private readonly ILogger<HealthStateMonitor> _logger;

    private readonly HealthStateTracker _tracker = new HealthStateTracker();

    private bool _hasPublished;

    /// <summary>
    /// Initializes a new instance of the <see cref="HealthStateMonitor"/> class.
    /// </summary>
    /// <param name="logger">The logger the state changes are written to.</param>
    /// <exception cref="ArgumentNullException"><paramref name="logger"/> is <see langword="null"/>.</exception>
    public HealthStateMonitor(ILogger<HealthStateMonitor> logger)
    {
        ArgumentNullException.ThrowIfNull(logger);

        _logger = logger;
    }

    /// <summary>
    /// Gets the current readiness state.
    /// </summary>
    /// <value>The state after the last report. The default is <see cref="HealthState.Starting"/>.</value>
    public HealthState Current => _tracker.Current;

    /// <inheritdoc/>
    /// <exception cref="ArgumentNullException"><paramref name="report"/> is <see langword="null"/>.</exception>
    public Task PublishAsync(HealthReport report, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(report);
        cancellationToken.ThrowIfCancellationRequested();

        if (!_hasPublished)
        {
            _hasPublished = true;

            // The first report is the moment the readiness signal starts answering; the state it
            // reports follows immediately below.
            _logger.HealthStarting();
        }

        var change = _tracker.Apply(report.Status);

        if (change == HealthState.Ready)
        {
            _logger.HealthReady();
        }
        else if (change == HealthState.NotReady)
        {
            _logger.HealthNotReady(report.Status.ToString(), FailedChecksOf(report));
        }

        return Task.CompletedTask;
    }

    private static string FailedChecksOf(HealthReport report)
    {
        var failed = report.Entries
            .Where(entry => entry.Value.Status != HealthStatus.Healthy)
            .Select(entry => entry.Key)
            .Order(StringComparer.Ordinal);

        return string.Join(", ", failed);
    }
}
