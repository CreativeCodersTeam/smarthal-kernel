using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SmartHal.Server.Diagnostics;

namespace SmartHal.Server.Hosting;

/// <summary>
/// Hooks the sub-system into the lifecycle of the host.
/// </summary>
public sealed class SubSystemHostedService : IHostedLifecycleService
{
    private readonly ILogger<SubSystemHostedService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="SubSystemHostedService"/> class.
    /// </summary>
    /// <param name="logger">The logger the lifecycle steps are written to.</param>
    /// <exception cref="ArgumentNullException"><paramref name="logger"/> is <see langword="null"/>.</exception>
    public SubSystemHostedService(ILogger<SubSystemHostedService> logger)
    {
        ArgumentNullException.ThrowIfNull(logger);

        _logger = logger;
    }

    /// <inheritdoc/>
    public Task StartingAsync(CancellationToken cancellationToken) => Log(nameof(StartingAsync));

    /// <inheritdoc/>
    public Task StartAsync(CancellationToken cancellationToken) => Log(nameof(StartAsync));

    /// <inheritdoc/>
    public Task StartedAsync(CancellationToken cancellationToken) => Log(nameof(StartedAsync));

    /// <inheritdoc/>
    public Task StoppingAsync(CancellationToken cancellationToken) => Log(nameof(StoppingAsync));

    /// <inheritdoc/>
    public Task StopAsync(CancellationToken cancellationToken) => Log(nameof(StopAsync));

    /// <inheritdoc/>
    public Task StoppedAsync(CancellationToken cancellationToken) => Log(nameof(StoppedAsync));

    private Task Log(string step)
    {
        _logger.SubSystemLifecycleStep(step);

        return Task.CompletedTask;
    }
}
