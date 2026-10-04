using System.Globalization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SmartHal.Server.Diagnostics;

namespace SmartHal.Server.Configuration;

/// <summary>
/// Validates the rules of <see cref="SmartHalOptions"/> that data annotations cannot express.
/// </summary>
/// <remarks>
/// All violations are reported at once. Validation has a side effect: a missing data directory is created.
/// </remarks>
public sealed class SmartHalOptionsValidator : IValidateOptions<SmartHalOptions>
{
    /// <summary>
    /// The maximum length of <see cref="SmartHalOptions.InstanceName"/>.
    /// </summary>
    public const int MaximumInstanceNameLength = 64;

    private readonly ILogger<SmartHalOptionsValidator> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="SmartHalOptionsValidator"/> class.
    /// </summary>
    /// <param name="logger">The logger the validator writes to.</param>
    /// <exception cref="ArgumentNullException"><paramref name="logger"/> is <see langword="null"/>.</exception>
    public SmartHalOptionsValidator(ILogger<SmartHalOptionsValidator> logger)
    {
        ArgumentNullException.ThrowIfNull(logger);

        _logger = logger;
    }

    /// <summary>
    /// Gets the maximum value of <see cref="SmartHalOptions.ShutdownTimeout"/>.
    /// </summary>
    /// <value>Five minutes.</value>
    public static TimeSpan MaximumShutdownTimeout { get; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Validates the bound <see cref="SmartHalOptions"/>.
    /// </summary>
    /// <param name="name">The name of the options instance.</param>
    /// <param name="options">The bound options.</param>
    /// <returns>The validation result, with one message per violation.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> is <see langword="null"/>.</exception>
    public ValidateOptionsResult Validate(string? name, SmartHalOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var failures = new List<string>();

        ValidateInstanceName(options.InstanceName, failures);
        ValidateShutdownTimeout(options.ShutdownTimeout, failures);
        ValidateDataDirectory(options.DataDirectory, failures);

        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }

    private static void ValidateInstanceName(string instanceName, List<string> failures)
    {
        if (string.IsNullOrWhiteSpace(instanceName))
        {
            failures.Add(Failure(
                nameof(SmartHalOptions.InstanceName),
                "must be set and must not consist of whitespace only."));

            return;
        }

        if (instanceName.Length > MaximumInstanceNameLength)
        {
            failures.Add(Failure(
                nameof(SmartHalOptions.InstanceName),
                "must not be longer than "
                + MaximumInstanceNameLength.ToString(CultureInfo.InvariantCulture)
                + " characters."));
        }
    }

    private static void ValidateShutdownTimeout(TimeSpan shutdownTimeout, List<string> failures)
    {
        if (shutdownTimeout <= TimeSpan.Zero)
        {
            failures.Add(Failure(nameof(SmartHalOptions.ShutdownTimeout), "must be greater than zero."));

            return;
        }

        if (shutdownTimeout > MaximumShutdownTimeout)
        {
            failures.Add(Failure(
                nameof(SmartHalOptions.ShutdownTimeout),
                "must not be longer than "
                + MaximumShutdownTimeout.ToString("c", CultureInfo.InvariantCulture)
                + "."));
        }
    }

    private void ValidateDataDirectory(string dataDirectory, List<string> failures)
    {
        if (string.IsNullOrWhiteSpace(dataDirectory))
        {
            failures.Add(Failure(
                nameof(SmartHalOptions.DataDirectory),
                "must be set and must not consist of whitespace only."));

            return;
        }

        string fullPath;

        try
        {
            fullPath = Path.GetFullPath(dataDirectory);
        }
        // Every one of these means the same thing here: the text is no path this machine can use.
        catch (Exception exception) when (IsPathFailure(exception))
        {
            failures.Add(Failure(nameof(SmartHalOptions.DataDirectory), "is not a valid path."));

            return;
        }

        if (!Directory.Exists(fullPath))
        {
            try
            {
                Directory.CreateDirectory(fullPath);
            }
            catch (Exception exception) when (IsPathFailure(exception))
            {
                failures.Add(Failure(
                    nameof(SmartHalOptions.DataDirectory),
                    "does not exist and could not be created."));

                return;
            }

            _logger.DataDirectoryCreated(fullPath);
        }

        if (!IsWritable(fullPath))
        {
            failures.Add(Failure(nameof(SmartHalOptions.DataDirectory), "exists but is not writable."));
        }
    }

    private static bool IsWritable(string directory)
    {
        var probeFile = Path.Combine(directory, $"smarthal-write-probe-{Guid.NewGuid():N}.tmp");

        try
        {
            // DeleteOnClose removes the probe file again on every platform, even when the process
            // ends between the two operations.
            using var stream = new FileStream(
                probeFile,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 1,
                FileOptions.DeleteOnClose);

            stream.WriteByte(0);

            return true;
        }
        catch (Exception exception) when (IsPathFailure(exception))
        {
            return false;
        }
    }

    private static bool IsPathFailure(Exception exception)
    {
        return exception is IOException
            or UnauthorizedAccessException
            or ArgumentException
            or NotSupportedException
            or System.Security.SecurityException;
    }

    private static string Failure(string field, string reason)
    {
        return $"{SmartHalOptions.SectionName}:{field}: {reason}";
    }
}
