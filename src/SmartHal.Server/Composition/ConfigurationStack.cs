using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.CommandLine;
using Microsoft.Extensions.Hosting;

namespace SmartHal.Server.Composition;

/// <summary>
/// Defines which configuration sources the server reads and in which order they override each other.
/// </summary>
// CA1711: the suffix "Stack" is reserved for types that derive from System.Collections.Stack. Here it
// names the layering of section 6.3, which is the term the specification and the plan use; renaming
// the type would break that shared vocabulary without making it clearer.
#pragma warning disable CA1711
public static class ConfigurationStack
#pragma warning restore CA1711
{
    /// <summary>
    /// The environment variable that names an additional configuration file.
    /// </summary>
    public const string ExternalConfigurationFileVariable = "SMARTHAL_CONFIG_FILE";

    /// <summary>
    /// The prefix of the environment variables that take part in the configuration.
    /// </summary>
    public const string EnvironmentVariablePrefix = "SMARTHAL_";

    private const string ExternalConfigurationFilePropertyKey = "SmartHal.ExternalConfigurationFile";

    /// <summary>
    /// Replaces the default configuration sources of the host with the ones of the server.
    /// </summary>
    /// <param name="builder">The host builder whose configuration is rebuilt.</param>
    /// <exception cref="ArgumentNullException"><paramref name="builder"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidDataException">
    /// <c>SMARTHAL_CONFIG_FILE</c> points at something that is not a readable JSON file.
    /// </exception>
    public static void Apply(HostApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        var commandLineArguments = CommandLineArgumentsOf(builder);
        var externalFile = ExternalConfigurationFile.Resolve(
            Environment.GetEnvironmentVariable(ExternalConfigurationFileVariable));
        ((IHostApplicationBuilder)builder).Properties[ExternalConfigurationFilePropertyKey] = externalFile;

        builder.Configuration.Sources.Clear();

        // 1 - appsettings.json, always.
        builder.Configuration.AddJsonFile("appsettings.json", optional: true, reloadOnChange: false);

        // 2 - appsettings.{Environment}.json, when present.
        builder.Configuration.AddJsonFile(
            $"appsettings.{builder.Environment.EnvironmentName}.json",
            optional: true,
            reloadOnChange: false);

        // 3 - the file of SMARTHAL_CONFIG_FILE, optional.
        if (externalFile.FilePath is not null)
        {
            builder.Configuration.AddJsonFile(externalFile.FilePath, optional: true, reloadOnChange: false);
        }

        // 4 - user secrets, in Development only.
        if (builder.Environment.IsDevelopment())
        {
            builder.Configuration.AddUserSecrets(
                typeof(ConfigurationStack).Assembly,
                optional: true,
                reloadOnChange: false);
        }

        // 5 - environment variables carrying the SMARTHAL_ prefix.
        builder.Configuration.AddEnvironmentVariables(EnvironmentVariablePrefix);

        // 6 - command line arguments.
        builder.Configuration.AddCommandLine(commandLineArguments);
    }

    /// <summary>
    /// Gets the state of the external configuration file, so it can be logged once a logger exists.
    /// </summary>
    /// <param name="builder">The host builder <see cref="Apply"/> was called on.</param>
    /// <returns>The state of the external configuration file.</returns>
    internal static ExternalConfigurationFile ExternalConfigurationFileOf(HostApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        return ((IHostApplicationBuilder)builder).Properties
                .TryGetValue(ExternalConfigurationFilePropertyKey, out var state)
            && state is ExternalConfigurationFile externalFile
            ? externalFile
            : ExternalConfigurationFile.NotConfigured;
    }

    private static string[] CommandLineArgumentsOf(HostApplicationBuilder builder)
    {
        var source = builder.Configuration.Sources.OfType<CommandLineConfigurationSource>().FirstOrDefault();

        return source?.Args?.ToArray() ?? [];
    }
}

/// <summary>
/// Describes the external configuration file named by <c>SMARTHAL_CONFIG_FILE</c>.
/// </summary>
/// <param name="FilePath">The configured path, or <see langword="null"/> when the variable is unset.</param>
/// <param name="Exists"><see langword="true"/> if a file exists at <paramref name="FilePath"/>; otherwise, <see langword="false"/>.</param>
internal sealed record ExternalConfigurationFile(string? FilePath, bool Exists)
{
    /// <summary>Gets the state for a process started without the environment variable.</summary>
    public static ExternalConfigurationFile NotConfigured { get; } = new ExternalConfigurationFile(null, false);

    /// <summary>
    /// Resolves the configured path before the configuration is built.
    /// </summary>
    /// <param name="filePath">The raw value of <c>SMARTHAL_CONFIG_FILE</c>.</param>
    /// <returns>The state of the configured file.</returns>
    /// <exception cref="InvalidDataException"><paramref name="filePath"/> names a directory.</exception>
    public static ExternalConfigurationFile Resolve(string? filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return NotConfigured;
        }

        if (Directory.Exists(filePath))
        {
            throw new InvalidDataException(
                $"The external configuration file '{filePath}' is a directory, not a readable JSON file.");
        }

        return new ExternalConfigurationFile(filePath, File.Exists(filePath));
    }
}
