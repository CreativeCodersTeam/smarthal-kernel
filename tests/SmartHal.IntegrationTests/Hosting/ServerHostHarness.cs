using System.Runtime.InteropServices;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using SmartHal.Server.Composition;
using SmartHal.Server.Diagnostics;
using SmartHal.Server.Health;
using SmartHal.Server.Hosting;

namespace SmartHal.IntegrationTests.Hosting;

/// <summary>
/// Starts the real server host in process, lets a test override its configuration and services, and
/// stops it again (FR-63, FR-64).
/// </summary>
/// <remarks>
/// <para>
/// The harness calls <see cref="ServerHost.RunAsync"/> in the background and returns as soon as
/// <see cref="HarnessProbeService"/> has started or <c>RunAsync</c> has ended - the latter is the
/// case when the start was aborted, for example by an invalid configuration.
/// </para>
/// <para>
/// Logs are collected through <c>AddFakeLogging</c>, so no test needs a Serilog type (C-6). The
/// harness sets environment variables process wide and restores them when it is disposed, so tests
/// using it belong in the <c>ProcessEnvironment</c> collection.
/// </para>
/// </remarks>
public sealed class ServerHostHarness : IAsyncDisposable
{
    private readonly HarnessOptions _options;

    private readonly Dictionary<string, string?> _originalEnvironmentVariables = new(StringComparer.Ordinal);

    private readonly TaskCompletionSource _startSignal = new(TaskCreationOptions.RunContinuationsAsynchronously);

    // The collector belongs to the harness rather than to the host, so the log of a start that was
    // aborted before its hosted services - an invalid configuration, for example - is readable too.
    private readonly FakeLogCollector _logCollector = FakeLogCollector.Create(new FakeLogCollectorOptions());

    private Task<int> _completion = Task.FromResult(ExitCodes.Success);

    private HarnessProbeService? _probe;

    private bool _disposed;

    private ServerHostHarness(HarnessOptions options)
    {
        _options = options;
        TempDirectory = Path.Combine(Path.GetTempPath(), $"smarthal-harness-{Guid.NewGuid():N}");
    }

    /// <summary>
    /// Gets the overrides this harness was started with.
    /// </summary>
    /// <value>The options the caller of <see cref="StartAsync"/> configured.</value>
    public HarnessOptions Options => _options;

    /// <summary>
    /// Gets the temporary directory that belongs to this harness.
    /// </summary>
    /// <value>An empty directory that is deleted when the harness is disposed.</value>
    public string TempDirectory { get; }

    /// <summary>
    /// Gets a value indicating whether the host started the probe service.
    /// </summary>
    /// <value><see langword="true"/> when the host reached its hosted services.</value>
    public bool Started => _probe?.HasStarted ?? false;

    /// <summary>
    /// Gets a value indicating whether the host stopped the probe service.
    /// </summary>
    /// <value><see langword="true"/> after an orderly shutdown.</value>
    public bool Stopped => _probe?.HasStopped ?? false;

    /// <summary>
    /// Gets the service provider of the running host.
    /// </summary>
    /// <value>The provider the host built.</value>
    /// <exception cref="InvalidOperationException">The host did not start.</exception>
    public IServiceProvider Services => _probe?.Services
        ?? throw new InvalidOperationException(
            "The host did not start, so its services are unavailable. Inspect Completion for the exit code.");

    /// <summary>
    /// Gets the log records the host wrote.
    /// </summary>
    /// <value>The collector of the fake logging provider; <c>GetSnapshot()</c> returns the records
    /// with their <c>EventId</c> in <c>Id</c>. It is readable even when the host never reached its
    /// hosted services.</value>
    public FakeLogCollector Logs => _logCollector;

    /// <summary>
    /// Gets the task that carries the exit code of the host.
    /// </summary>
    /// <value>The result of <see cref="ServerHost.RunAsync"/>.</value>
    public Task<int> Completion => _completion;

    private string LogDirectory => Path.Combine(TempDirectory, "logs");

    /// <summary>
    /// Starts the server host with the given overrides.
    /// </summary>
    /// <param name="configure">An optional callback that fills the <see cref="HarnessOptions"/>.</param>
    /// <param name="cancellationToken">Stops the host when it is cancelled.</param>
    /// <returns>The started harness; the caller disposes it.</returns>
    public static async Task<ServerHostHarness> StartAsync(
        Action<HarnessOptions>? configure = null,
        CancellationToken cancellationToken = default)
    {
        var options = new HarnessOptions();
        configure?.Invoke(options);

        var harness = new ServerHostHarness(options);
        await harness.RunAsync(cancellationToken).ConfigureAwait(false);

        return harness;
    }

    /// <summary>
    /// Asks the host to shut down and waits for its exit code.
    /// </summary>
    /// <returns>The exit code of <see cref="ServerHost.RunAsync"/>.</returns>
    public async Task<int> StopAsync()
    {
        if (_probe is not null && !_completion.IsCompleted)
        {
            _probe.Services.GetRequiredService<IHostApplicationLifetime>().StopApplication();
        }

        return await _completion.ConfigureAwait(false);
    }

    /// <summary>
    /// Hands one shutdown signal to the running host and waits for its exit code.
    /// </summary>
    /// <param name="signal">The signal the host is to handle, for example <c>SIGTERM</c>.</param>
    /// <returns>The exit code of <see cref="ServerHost.RunAsync"/>.</returns>
    /// <exception cref="InvalidOperationException">The host did not start.</exception>
    /// <remarks>
    /// The signal is handed to <see cref="ShutdownSignalHandler"/> in process instead of being sent
    /// to the operating system, so the same test runs on Linux, macOS and Windows - which knows no
    /// <c>SIGTERM</c> (C-4, Constraint 16). A second <c>SIGINT</c> therefore records its exit code in
    /// <see cref="HarnessOptions.TerminateRecorder"/> rather than ending the test run.
    /// </remarks>
    public async Task<int> SignalAsync(PosixSignal signal)
    {
        Services.GetRequiredService<ShutdownSignalHandler>().Handle(signal);

        return await _completion.ConfigureAwait(false);
    }

    /// <summary>
    /// Waits until the host has reported that it is ready.
    /// </summary>
    /// <param name="timeout">How long to wait; ten seconds when it is omitted.</param>
    /// <returns>A task that completes as soon as the host is ready.</returns>
    /// <exception cref="TimeoutException">
    /// The host was not ready within <paramref name="timeout"/>; the message carries an excerpt of
    /// the log, so the test reports what the host did instead.
    /// </exception>
    /// <remarks>
    /// Both halves of the readiness signal have to agree: the log must carry the event of the ready
    /// state (FR-42), and <c>HealthCheckService</c> must report the <c>ready</c> checks as passing
    /// (FR-40). The state is polled rather than waited for, because the publisher reports on its own
    /// schedule and the first report follows half a second after the start.
    /// </remarks>
    public async Task WaitForReadyAsync(TimeSpan? timeout = null)
    {
        var deadline = DateTimeOffset.UtcNow + (timeout ?? TimeSpan.FromSeconds(10));

        while (true)
        {
            if (await IsReadyAsync().ConfigureAwait(false))
            {
                return;
            }

            if (DateTimeOffset.UtcNow >= deadline)
            {
                throw new TimeoutException(
                    $"The host did not become ready within {timeout ?? TimeSpan.FromSeconds(10)}. Log:"
                    + Environment.NewLine
                    + LogExcerpt());
            }

            await Task.Delay(TimeSpan.FromMilliseconds(25)).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Reads back every log event Serilog wrote to the redirected rolling file.
    /// </summary>
    /// <returns>
    /// One <see cref="JsonElement"/> per line of every file below <c>logs</c>, in file and line
    /// order; an empty list when the host wrote nothing.
    /// </returns>
    /// <exception cref="InvalidOperationException">
    /// The host is still running, or <see cref="HarnessOptions.CaptureSerilogJson"/> is
    /// <see langword="false"/>.
    /// </exception>
    /// <remarks>
    /// The file sink keeps its file open and flushes it when the host is disposed, so the content is
    /// only complete once <see cref="Completion"/> has finished. Reading raw text and parsing it with
    /// <see cref="System.Text.Json"/> keeps every Serilog type out of <c>tests/</c> (C-6).
    /// </remarks>
    public IReadOnlyList<JsonElement> ReadSerilogJsonLog()
    {
        if (!_options.CaptureSerilogJson)
        {
            throw new InvalidOperationException(
                "The harness did not redirect Serilog's file sink. Leave HarnessOptions.CaptureSerilogJson at its default to read the log.");
        }

        if (!_completion.IsCompleted)
        {
            throw new InvalidOperationException(
                "The host is still running, so its log file is not flushed yet. Await StopAsync() before reading the log.");
        }

        if (!Directory.Exists(LogDirectory))
        {
            return [];
        }

        var logEvents = new List<JsonElement>();

        foreach (var file in Directory.GetFiles(LogDirectory).Order(StringComparer.Ordinal))
        {
            foreach (var line in File.ReadAllLines(file))
            {
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                using var document = JsonDocument.Parse(line);
                logEvents.Add(document.RootElement.Clone());
            }
        }

        return logEvents;
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        if (_probe is not null && !_completion.IsCompleted)
        {
            _probe.Services.GetRequiredService<IHostApplicationLifetime>().StopApplication();
        }

        // Task.WhenAny never rethrows, so a host that ended in an error does not turn disposal into
        // the failure the test reports; reading Exception marks that error as observed.
        await Task.WhenAny(_completion).ConfigureAwait(false);
        _ = _completion.Exception;

        RestoreEnvironmentVariables();
        DeleteTempDirectory();
    }

    private async Task<bool> IsReadyAsync()
    {
        if (_probe?.Services is not { } services)
        {
            return false;
        }

        if (!_logCollector.GetSnapshot().Any(record => record.Id.Id == LogEvents.Health.HealthReady))
        {
            return false;
        }

        var report = await services.GetRequiredService<HealthCheckService>()
            .CheckHealthAsync(registration => registration.Tags.Contains(HealthTags.Ready), CancellationToken.None)
            .ConfigureAwait(false);

        return report.Status == HealthStatus.Healthy;
    }

    private string LogExcerpt()
    {
        var lines = _logCollector.GetSnapshot()
            .Select(record => $"{record.Level} {record.Id.Id} {record.Message}");

        return string.Join(Environment.NewLine, lines);
    }

    private async Task RunAsync(CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(TempDirectory);
        ApplyEnvironmentVariables();

        if (_options.UseTemporaryDataDirectory)
        {
            // TryAdd, so a test that deliberately configures one of the two keys - or removes it by
            // setting it to null - keeps what it set.
            _options.Configuration.TryAdd("SmartHal:InstanceName", "test");
            _options.Configuration.TryAdd("SmartHal:DataDirectory", Path.Combine(TempDirectory, "data"));
        }

        if (_options.CaptureSerilogJson)
        {
            _options.Configuration["Serilog:WriteTo:File:Args:path"] =
                Path.Combine(LogDirectory, "smarthal-.log");
            _options.Configuration["Serilog:WriteTo:File:Args:formatter"] =
                "Serilog.Formatting.Compact.CompactJsonFormatter, Serilog.Formatting.Compact";
        }

        string[] args = [.. _options.Args, "--environment", _options.EnvironmentName];

        _completion = Task.Run(
            () => ServerHost.RunAsync(args, ConfigureBuilder, cancellationToken),
            cancellationToken);

        await Task.WhenAny(_startSignal.Task, _completion).ConfigureAwait(false);
    }

    private void ConfigureBuilder(HostApplicationBuilder builder)
    {
        // Source 7: appended behind the command line so an override always wins.
        builder.Configuration.AddInMemoryCollection(_options.Configuration);
        builder.Services.AddFakeLogging();

        // Registered after AddFakeLogging so that the fake provider writes into the collector this
        // harness owns, whichever way the package registers its own.
        builder.Services.AddSingleton(_logCollector);
        // Registered after the composition root, so this handler is the one the host resolves: it
        // records the exit code of an aborted shutdown instead of ending the test run (C-4).
        builder.Services.AddSingleton(serviceProvider => new ShutdownSignalHandler(
            serviceProvider.GetRequiredService<IHostApplicationLifetime>(),
            serviceProvider.GetRequiredService<ILogger<ShutdownSignalHandler>>(),
            _options.TerminateRecorder.Record));

        builder.Services.AddHostedService(serviceProvider =>
        {
            var probe = new HarnessProbeService(serviceProvider, _startSignal);
            _probe = probe;

            return probe;
        });

        _options.ConfigureServices?.Invoke(builder.Services);
    }

    private void ApplyEnvironmentVariables()
    {
        foreach (var variable in _options.EnvironmentVariables)
        {
            _originalEnvironmentVariables[variable.Key] = Environment.GetEnvironmentVariable(variable.Key);
            Environment.SetEnvironmentVariable(variable.Key, variable.Value);
        }
    }

    private void RestoreEnvironmentVariables()
    {
        foreach (var variable in _originalEnvironmentVariables)
        {
            Environment.SetEnvironmentVariable(variable.Key, variable.Value);
        }

        _originalEnvironmentVariables.Clear();
    }

    private void DeleteTempDirectory()
    {
        if (!Directory.Exists(TempDirectory))
        {
            return;
        }

        // A temporary directory that a still open handle keeps alive must not fail the test that
        // merely used it; the operating system reclaims it either way.
        try
        {
            Directory.Delete(TempDirectory, recursive: true);
        }
        catch (IOException)
        {
            // Ignored on purpose, see above.
        }
        catch (UnauthorizedAccessException)
        {
            // Ignored on purpose, see above.
        }
    }
}
