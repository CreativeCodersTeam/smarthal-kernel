using System.Runtime.InteropServices;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.CommandLine;
using Microsoft.Extensions.Configuration.EnvironmentVariables;
using Microsoft.Extensions.Configuration.Json;
using Microsoft.Extensions.Configuration.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SmartHal.Server.Configuration;
using SmartHal.Server.Diagnostics;
using SmartHal.Server.Hosting;

namespace SmartHal.Server.Composition;

/// <summary>
/// The composition root of the server process: it runs the start sequence of section 6.5 and
/// returns its exit code (IF-5, FR-13, FR-17).
/// </summary>
public static class ServerHost
{
    /// <summary>
    /// Runs the server until it is asked to shut down and returns the process exit code.
    /// </summary>
    /// <param name="args">The command line arguments of the process; they form source 6 of the
    /// configuration stack and carry the host settings such as <c>--environment</c>.</param>
    /// <param name="configure">An optional callback that runs after the composition root and before
    /// the host is built. Tests use it to add overrides; it is <see langword="null"/> in production.</param>
    /// <param name="cancellationToken">Stops the host when it is cancelled.</param>
    /// <returns>
    /// <see cref="ExitCodes.Success"/> after an orderly shutdown, <see cref="ExitCodes.UnhandledError"/>
    /// after an unhandled error while starting, and <see cref="ExitCodes.InvalidConfiguration"/>
    /// when the configuration could not be built or breaks a validation rule.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="args"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// Step 1 of the sequence runs before any logger exists, so a failure there is reported on
    /// standard error instead of the log (G-10). Every configuration event is therefore written once
    /// the host is built and Serilog stands behind the logger factory. Step 4 validates the bound
    /// options; the host runs that check before the first hosted service, so an invalid
    /// configuration ends the process without any of them having started (FR-23). Step 7 waits for
    /// the shutdown request of a signal, of the lifetime or of <paramref name="cancellationToken"/>,
    /// and step 8 stops the host exactly once, bounded by <c>SmartHal:ShutdownTimeout</c> plus a
    /// second of grace (FR-15, FR-16).
    /// </remarks>
    public static async Task<int> RunAsync(
        string[] args,
        Action<HostApplicationBuilder>? configure = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(args);

        HostApplicationBuilder builder;
        ExternalConfigurationFile externalFile;

        try
        {
            // Step 1 - build the configuration (section 6.3).
            builder = Host.CreateApplicationBuilder(args);
            ConfigurationStack.Apply(builder);
            externalFile = ConfigurationStack.ExternalConfigurationFileOf(builder);
        }
        // The configuration sources report an unusable file through a whole family of I/O and format
        // exceptions; every one of them means the same thing here, namely exit code 2 (G-10).
        catch (Exception exception)
        {
            await Console.Error
                .WriteLineAsync($"The SmartHal configuration could not be built: {exception.Message}")
                .ConfigureAwait(false);

            return ExitCodes.InvalidConfiguration;
        }

        // Step 2 - put Serilog behind ILogger<T>; it is built from the Serilog section (FR-28).
        SerilogSetup.Configure(builder);

        // Step 3 - the composition root registers its services: the options first, because the
        // readiness check of FR-43 reports exactly their binding and validation.
        builder.Services.AddSmartHalOptions(builder.Configuration);
        builder.Services.AddSmartHalHealth();
        builder.Services.AddSmartHalHosting();

        configure?.Invoke(builder);

        var environmentName = builder.Environment.EnvironmentName;
        var configurationSources = DescribeConfigurationSources(builder.Configuration.Sources);

        using var host = builder.Build();

        var logger = host.Services.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(ServerHost));
        logger.HostStarting(environmentName);
        logger.ConfigurationSourcesLoaded(configurationSources);
        ReportExternalConfigurationFile(logger, externalFile);

        try
        {
            // Step 5 - start the host and its hosted services.
            await host.StartAsync(cancellationToken).ConfigureAwait(false);
        }
        // Step 4 - the host validates the bound options before it reaches a hosted service, so an
        // invalid configuration arrives here and ends the process with exit code 2 (FR-23).
        catch (OptionsValidationException exception)
        {
            foreach (var failure in OptionsFailure.Parse(exception))
            {
                logger.ConfigurationInvalid(failure.Section, failure.Field, failure.Reason);
            }

            return ExitCodes.InvalidConfiguration;
        }
        // Any error a hosted service raises while starting ends the process with exit code 1.
        catch (Exception exception)
        {
            logger.HostFailed(exception);

            return ExitCodes.UnhandledError;
        }

        logger.HostStarted();

        // Step 7 - the shutdown signals reach the host through the handler (FR-15). Cancelling the
        // signal keeps the runtime from ending the process itself, which would skip the orderly
        // shutdown below; the registrations end with this method, so they never outlive the host.
        var signalHandler = host.Services.GetRequiredService<ShutdownSignalHandler>();

        using var sigtermRegistration = PosixSignalRegistration.Create(
            PosixSignal.SIGTERM,
            context =>
            {
                context.Cancel = true;
                signalHandler.Handle(context.Signal);
            });

        using var sigintRegistration = PosixSignalRegistration.Create(
            PosixSignal.SIGINT,
            context =>
            {
                context.Cancel = true;
                signalHandler.Handle(context.Signal);
            });

        await WaitForShutdownRequestAsync(host.Services, cancellationToken).ConfigureAwait(false);

        // Step 8 - stop the host exactly once, bounded by the configured span plus a second of grace
        // (FR-16). The host cancels the token of its hosted services once the span has passed, and
        // the extra second leaves a service that observes it the chance to end on its own; a service
        // that ignores the token is abandoned here, because nothing else ends the process then.
        var shutdownTimeout = host.Services.GetRequiredService<IOptions<SmartHalOptions>>().Value.ShutdownTimeout;

        try
        {
            // Not cancellationToken: the token that asked for the shutdown must not also abort it -
            // the timeout above is what bounds the stop.
            await host.StopAsync(CancellationToken.None)
                .WaitAsync(shutdownTimeout + TimeSpan.FromSeconds(1), CancellationToken.None)
                .ConfigureAwait(false);
        }
        // The stop is abandoned rather than the process killed, and the exit code stays 0: an
        // exceeded timeout is a warning rather than an error (G-17).
        catch (TimeoutException)
        {
            logger.ShutdownTimeoutExceeded(shutdownTimeout);
        }

        logger.HostStopped();

        return ExitCodes.Success;
    }

    private static async Task WaitForShutdownRequestAsync(
        IServiceProvider services,
        CancellationToken cancellationToken)
    {
        var lifetime = services.GetRequiredService<IHostApplicationLifetime>();
        var shutdownRequested = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        // The request arrives either from the lifetime - a signal, or a component that stops the
        // application - or from the token this method was called with. Waiting for it here instead of
        // through WaitForShutdownAsync keeps the stop itself in step 8, where the timeout bounds it.
        using var lifetimeRegistration = lifetime.ApplicationStopping.Register(
            () => shutdownRequested.TrySetResult());
        using var cancellationRegistration = cancellationToken.Register(
            () => shutdownRequested.TrySetResult());

        await shutdownRequested.Task.ConfigureAwait(false);
    }

    private static string DescribeConfigurationSources(IEnumerable<IConfigurationSource> sources)
    {
        return string.Join(", ", sources.Select(DescribeConfigurationSource));
    }

    private static string DescribeConfigurationSource(IConfigurationSource source)
    {
        return source switch
        {
            JsonConfigurationSource json => json.Path ?? "JSON file",
            EnvironmentVariablesConfigurationSource environmentVariables =>
                $"environment variables ({environmentVariables.Prefix})",
            CommandLineConfigurationSource => "command line",
            MemoryConfigurationSource => "memory",
            _ => source.GetType().Name
        };
    }

    private static void ReportExternalConfigurationFile(ILogger logger, ExternalConfigurationFile externalFile)
    {
        if (externalFile.FilePath is null)
        {
            return;
        }

        if (externalFile.Exists)
        {
            logger.ExternalConfigurationFileLoaded(externalFile.FilePath);
        }
        else
        {
            logger.ExternalConfigurationFileSkipped(externalFile.FilePath);
        }
    }
}
