using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;

namespace SmartHal.Server.Diagnostics;

/// <summary>
/// Defines the fixed <c>EventId</c>s and log methods of the server, so its events stay machine readable.
/// </summary>
/// <remarks>
/// Each area owns a range of a hundred IDs within <c>1000-1999</c>.
/// </remarks>
public static partial class LogEvents
{
// S3218: each constant deliberately carries the same name as the log method that writes it, because
// the table of section 6.6 names the event only once. The shadowing is what keeps the number and the
// method that uses it visibly paired; the nested class makes both reachable without ambiguity.
#pragma warning disable S3218

    /// <summary>The lifecycle events of the server process (<c>1000-1099</c>).</summary>
    public static class Lifecycle
    {
        /// <summary>The host is about to start.</summary>
        public const int HostStarting = 1000;

        /// <summary>The host and all hosted services have started.</summary>
        public const int HostStarted = 1001;

        /// <summary>A shutdown signal was received.</summary>
        public const int ShutdownRequested = 1002;

        /// <summary>The host has stopped.</summary>
        public const int HostStopped = 1003;

        /// <summary>The shutdown took longer than the configured timeout.</summary>
        public const int ShutdownTimeoutExceeded = 1004;

        /// <summary>A second interrupt signal aborted the orderly shutdown.</summary>
        public const int ShutdownAborted = 1005;

        /// <summary>An unhandled error ended the process with <see cref="Composition.ExitCodes.UnhandledError"/>.</summary>
        public const int HostFailed = 1006;
    }

    /// <summary>The configuration events of the server process (<c>1100-1199</c>).</summary>
    public static class Configuration
    {
        /// <summary>The configuration sources were built, in order.</summary>
        public const int ConfigurationSourcesLoaded = 1100;

        /// <summary>The external configuration file was skipped.</summary>
        public const int ExternalConfigurationFileSkipped = 1101;

        /// <summary>The external configuration file was loaded.</summary>
        public const int ExternalConfigurationFileLoaded = 1102;

        /// <summary>The bound configuration breaks a validation rule.</summary>
        public const int ConfigurationInvalid = 1103;

        /// <summary>The data directory did not exist and was created.</summary>
        public const int DataDirectoryCreated = 1104;
    }

    /// <summary>The health events of the server process (<c>1200-1299</c>).</summary>
    public static class Health
    {
        /// <summary>The readiness checks have started.</summary>
        public const int HealthStarting = 1200;

        /// <summary>All readiness checks pass; the process is ready.</summary>
        public const int HealthReady = 1201;

        /// <summary>At least one readiness check fails.</summary>
        public const int HealthNotReady = 1202;
    }

    /// <summary>The sub-system events of the server process (<c>1300-1399</c>).</summary>
    public static class SubSystem
    {
        /// <summary>The sub-system passed a step of the host lifecycle.</summary>
        public const int SubSystemLifecycleStep = 1300;
    }

#pragma warning restore S3218

    /// <summary>
    /// Writes that the host is starting in the given environment.
    /// </summary>
    /// <param name="logger">The logger the event is written to.</param>
    /// <param name="environmentName">The resolved host environment, for example <c>Production</c>.</param>
    [LoggerMessage(
        EventId = Lifecycle.HostStarting,
        Level = LogLevel.Information,
        Message = "The SmartHal server is starting in environment '{EnvironmentName}'.")]
    public static partial void HostStarting(this ILogger logger, string environmentName);

    /// <summary>
    /// Writes that the host and all hosted services have started.
    /// </summary>
    /// <param name="logger">The logger the event is written to.</param>
    [LoggerMessage(
        EventId = Lifecycle.HostStarted,
        Level = LogLevel.Information,
        Message = "The SmartHal server has started.")]
    public static partial void HostStarted(this ILogger logger);

    /// <summary>
    /// Writes that a shutdown signal arrived and the orderly shutdown has begun.
    /// </summary>
    /// <param name="logger">The logger the event is written to.</param>
    /// <param name="signal">The signal that arrived, for example <see cref="PosixSignal.SIGTERM"/>.</param>
    [LoggerMessage(
        EventId = Lifecycle.ShutdownRequested,
        Level = LogLevel.Information,
        Message = "The SmartHal server received the signal '{Signal}' and is shutting down.")]
    public static partial void ShutdownRequested(this ILogger logger, PosixSignal signal);

    /// <summary>
    /// Writes that the host has stopped.
    /// </summary>
    /// <param name="logger">The logger the event is written to.</param>
    [LoggerMessage(
        EventId = Lifecycle.HostStopped,
        Level = LogLevel.Information,
        Message = "The SmartHal server has stopped.")]
    public static partial void HostStopped(this ILogger logger);

    /// <summary>
    /// Writes that the orderly shutdown took longer than the configured timeout.
    /// </summary>
    /// <param name="logger">The logger the event is written to.</param>
    /// <param name="shutdownTimeout">The span of <c>SmartHal:ShutdownTimeout</c> that was exceeded.</param>
    [LoggerMessage(
        EventId = Lifecycle.ShutdownTimeoutExceeded,
        Level = LogLevel.Warning,
        Message = "The orderly shutdown took longer than the configured timeout of {ShutdownTimeout}; the SmartHal server ends anyway.")]
    public static partial void ShutdownTimeoutExceeded(this ILogger logger, TimeSpan shutdownTimeout);

    /// <summary>
    /// Writes that a second interrupt signal aborted the orderly shutdown.
    /// </summary>
    /// <param name="logger">The logger the event is written to.</param>
    /// <param name="exitCode">The exit code the process ends with.</param>
    [LoggerMessage(
        EventId = Lifecycle.ShutdownAborted,
        Level = LogLevel.Warning,
        Message = "A second interrupt signal aborted the orderly shutdown; the SmartHal server ends with exit code {ExitCode}.")]
    public static partial void ShutdownAborted(this ILogger logger, int exitCode);

    /// <summary>
    /// Writes that an unhandled error ended the start of the host.
    /// </summary>
    /// <param name="logger">The logger the event is written to.</param>
    /// <param name="exception">The error that ended the start.</param>
    [LoggerMessage(
        EventId = Lifecycle.HostFailed,
        Level = LogLevel.Critical,
        Message = "The SmartHal server failed with an unhandled error.")]
    public static partial void HostFailed(this ILogger logger, Exception exception);

    /// <summary>
    /// Writes the configuration sources in the order in which they were applied.
    /// </summary>
    /// <param name="logger">The logger the event is written to.</param>
    /// <param name="configurationSources">The sources, earliest first.</param>
    [LoggerMessage(
        EventId = Configuration.ConfigurationSourcesLoaded,
        Level = LogLevel.Debug,
        Message = "Configuration sources loaded, earliest first: {ConfigurationSources}.")]
    public static partial void ConfigurationSourcesLoaded(this ILogger logger, string configurationSources);

    /// <summary>
    /// Writes that the external configuration file was skipped because it does not exist.
    /// </summary>
    /// <param name="logger">The logger the event is written to.</param>
    /// <param name="externalConfigurationFile">The path taken from <c>SMARTHAL_CONFIG_FILE</c>.</param>
    [LoggerMessage(
        EventId = Configuration.ExternalConfigurationFileSkipped,
        Level = LogLevel.Information,
        Message = "The external configuration file '{ExternalConfigurationFile}' does not exist and was skipped.")]
    public static partial void ExternalConfigurationFileSkipped(
        this ILogger logger,
        string externalConfigurationFile);

    /// <summary>
    /// Writes that the external configuration file was loaded.
    /// </summary>
    /// <param name="logger">The logger the event is written to.</param>
    /// <param name="externalConfigurationFile">The path taken from <c>SMARTHAL_CONFIG_FILE</c>.</param>
    [LoggerMessage(
        EventId = Configuration.ExternalConfigurationFileLoaded,
        Level = LogLevel.Information,
        Message = "The external configuration file '{ExternalConfigurationFile}' was loaded.")]
    public static partial void ExternalConfigurationFileLoaded(
        this ILogger logger,
        string externalConfigurationFile);

    /// <summary>
    /// Writes one broken validation rule of the bound configuration.
    /// </summary>
    /// <param name="logger">The logger the event is written to.</param>
    /// <param name="section">The configuration section the field belongs to, for example <c>SmartHal</c>.</param>
    /// <param name="field">The name of the field that breaks the rule.</param>
    /// <param name="reason">The rule that was broken, in plain words.</param>
    [LoggerMessage(
        EventId = Configuration.ConfigurationInvalid,
        Level = LogLevel.Error,
        Message = "The configuration is invalid: section '{Section}', field '{Field}' - {Reason}")]
    public static partial void ConfigurationInvalid(
        this ILogger logger,
        string section,
        string field,
        string reason);

    /// <summary>
    /// Writes that the data directory did not exist and was created.
    /// </summary>
    /// <param name="logger">The logger the event is written to.</param>
    /// <param name="dataDirectory">The absolute path of the directory that was created.</param>
    [LoggerMessage(
        EventId = Configuration.DataDirectoryCreated,
        Level = LogLevel.Information,
        Message = "The data directory '{DataDirectory}' did not exist and was created.")]
    public static partial void DataDirectoryCreated(this ILogger logger, string dataDirectory);

    /// <summary>
    /// Writes that the readiness checks have started to report.
    /// </summary>
    /// <param name="logger">The logger the event is written to.</param>
    [LoggerMessage(
        EventId = Health.HealthStarting,
        Level = LogLevel.Information,
        Message = "The readiness checks have started to report; the SmartHal server is starting.")]
    public static partial void HealthStarting(this ILogger logger);

    /// <summary>
    /// Writes that every readiness check passes.
    /// </summary>
    /// <param name="logger">The logger the event is written to.</param>
    [LoggerMessage(
        EventId = Health.HealthReady,
        Level = LogLevel.Information,
        Message = "Every readiness check passes; the SmartHal server is ready.")]
    public static partial void HealthReady(this ILogger logger);

    /// <summary>
    /// Writes that the process is no longer ready.
    /// </summary>
    /// <param name="logger">The logger the event is written to.</param>
    /// <param name="healthStatus">The combined result of the readiness checks, for example <c>Unhealthy</c>.</param>
    /// <param name="failedChecks">The names of the checks that no longer pass, separated by commas.</param>
    [LoggerMessage(
        EventId = Health.HealthNotReady,
        Level = LogLevel.Warning,
        Message = "The SmartHal server is not ready: the readiness checks report '{HealthStatus}'; failing checks: {FailedChecks}.")]
    public static partial void HealthNotReady(this ILogger logger, string healthStatus, string failedChecks);

    /// <summary>
    /// Writes that the sub-system passed a step of the host lifecycle.
    /// </summary>
    /// <param name="logger">The logger the event is written to.</param>
    /// <param name="step">The lifecycle method that ran, for example <c>StartAsync</c>.</param>
    [LoggerMessage(
        EventId = SubSystem.SubSystemLifecycleStep,
        Level = LogLevel.Debug,
        Message = "The sub-system passed the lifecycle step '{Step}'.")]
    public static partial void SubSystemLifecycleStep(this ILogger logger, string step);
}
