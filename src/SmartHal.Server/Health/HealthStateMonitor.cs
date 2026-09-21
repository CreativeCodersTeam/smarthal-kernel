using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using SmartHal.Server.Diagnostics;

namespace SmartHal.Server.Health;

/// <summary>
/// Writes the readiness state of the process to the log whenever it changes (FR-40, FR-42).
/// </summary>
/// <remarks>
/// <para>
/// The health check infrastructure runs the readiness checks on its own schedule and hands every
/// result to this publisher, which is why the state changes are detected here rather than in a
/// service of its own (G-8). The schedule itself is set in
/// <see cref="Composition.ServiceRegistration.AddSmartHalHealth"/>.
/// </para>
/// <para>
/// FR-41 leaves the log as the only way to observe the state from outside the process, so each
/// transition is written exactly once, with the <c>EventId</c> of section 6.6: <c>1200</c> when the
/// signal opens, <c>1201</c> for ready and <c>1202</c> for no longer ready. A report that does not
/// change the state writes nothing, so a healthy process stays quiet.
/// </para>
/// </remarks>
public sealed class HealthStateMonitor : IHealthCheckPublisher
{
    private readonly ILogger<HealthStateMonitor> _logger;

    private readonly HealthStateTracker _tracker = new();

    private bool _hasPublished;

    /// <summary>
    /// Initialises a new instance of the <see cref="HealthStateMonitor"/> class.
    /// </summary>
    /// <param name="logger">The logger the state changes are written to.</param>
    /// <exception cref="ArgumentNullException"><paramref name="logger"/> is <see langword="null"/>.</exception>
    public HealthStateMonitor(ILogger<HealthStateMonitor> logger)
    {
        ArgumentNullException.ThrowIfNull(logger);

        _logger = logger;
    }

    /// <summary>
    /// Gets the readiness state the last report left the process in.
    /// </summary>
    /// <value><see cref="HealthState.Starting"/> until a readiness check has passed for the first time.</value>
    public HealthState Current => _tracker.Current;

    /// <summary>
    /// Applies one result of the readiness checks and writes the state change it causes.
    /// </summary>
    /// <param name="report">The result of the readiness checks the schedule selected.</param>
    /// <param name="cancellationToken">Cancels the publication.</param>
    /// <returns>A completed task; the publisher only writes to the log.</returns>
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
