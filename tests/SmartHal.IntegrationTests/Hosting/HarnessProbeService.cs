using Microsoft.Extensions.Hosting;

namespace SmartHal.IntegrationTests.Hosting;

/// <summary>
/// The hosted service through which <see cref="ServerHostHarness"/> observes the host: it reports
/// the start, the stop and the running <see cref="IServiceProvider"/>.
/// </summary>
/// <remarks>
/// It is a plain test class rather than a fake, because the lifecycle behaviour is the subject under
/// test (plan section 3, mock boundary d).
/// </remarks>
public sealed class HarnessProbeService : IHostedService
{
    private readonly TaskCompletionSource _startSignal;

    /// <summary>
    /// Initialises a new instance of the <see cref="HarnessProbeService"/> class.
    /// </summary>
    /// <param name="services">The service provider of the running host.</param>
    /// <param name="startSignal">The signal the harness waits on until the host has started.</param>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public HarnessProbeService(IServiceProvider services, TaskCompletionSource startSignal)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(startSignal);

        Services = services;
        _startSignal = startSignal;
    }

    /// <summary>
    /// Gets the service provider of the running host.
    /// </summary>
    /// <value>The provider the host injected when it created this service.</value>
    public IServiceProvider Services { get; }

    /// <summary>
    /// Gets a value indicating whether the host has started this service.
    /// </summary>
    /// <value><see langword="true"/> once <see cref="StartAsync"/> has run.</value>
    public bool HasStarted { get; private set; }

    /// <summary>
    /// Gets a value indicating whether the host has stopped this service.
    /// </summary>
    /// <value><see langword="true"/> once <see cref="StopAsync"/> has run.</value>
    public bool HasStopped { get; private set; }

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken)
    {
        HasStarted = true;
        _startSignal.TrySetResult();

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken)
    {
        HasStopped = true;

        return Task.CompletedTask;
    }
}
