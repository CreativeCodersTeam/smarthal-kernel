using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.CommandLine;
using Microsoft.Extensions.Hosting;

namespace SmartHal.Server.Composition;

/// <summary>
/// Builds the layered configuration of section 6.3 on a <see cref="HostApplicationBuilder"/>
/// (IF-3, FR-18, FR-19, FR-20).
/// </summary>
/// <remarks>
/// <para>
/// <see cref="Apply"/> empties the sources the generic host has set up and adds exactly the six
/// sources of the table, earliest first; a later source overrides an earlier one. Unprefixed
/// environment variables - the default of the generic host - are deliberately not loaded (G-11).
/// </para>
/// <para>
/// The environment name is not part of the stack. It comes from the host configuration
/// (<c>DOTNET_ENVIRONMENT</c> or <c>--environment</c>) and is already resolved when
/// <see cref="Apply"/> runs.
/// </para>
/// </remarks>
// CA1711: the suffix "Stack" is reserved for types that derive from System.Collections.Stack. Here it
// names the layering of section 6.3, which is the term the specification and the plan use; renaming
// the type would break that shared vocabulary without making it clearer.
#pragma warning disable CA1711
public static class ConfigurationStack
#pragma warning restore CA1711
{
    /// <summary>
    /// The environment variable that names an additional configuration file (source 3).
    /// </summary>
    public const string ExternalConfigurationFileVariable = "SMARTHAL_CONFIG_FILE";

    /// <summary>
    /// The prefix every configuration environment variable carries (source 5, FR-19).
    /// </summary>
    public const string EnvironmentVariablePrefix = "SMARTHAL_";

    private const string ExternalConfigurationFilePropertyKey = "SmartHal.ExternalConfigurationFile";

    /// <summary>
    /// Replaces the configuration sources of <paramref name="builder"/> with the six sources of
    /// section 6.3.
    /// </summary>
    /// <param name="builder">The host builder whose configuration is rebuilt.</param>
    /// <exception cref="ArgumentNullException"><paramref name="builder"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidDataException">
    /// <c>SMARTHAL_CONFIG_FILE</c> points at something that is not a readable JSON file.
    /// </exception>
    /// <remarks>
    /// The command line arguments are taken from the source the generic host has already created,
    /// because <see cref="HostApplicationBuilder"/> does not expose them. The source is added again
    /// unconditionally, so source 6 exists even when the process was started without arguments.
    /// Whether the external configuration file was skipped or loaded is remembered in
    /// <see cref="IHostApplicationBuilder.Properties"/>; the matching event can only be written once
    /// the logger exists.
    /// </remarks>
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
    /// Returns what <see cref="Apply"/> found out about the external configuration file.
    /// </summary>
    /// <param name="builder">The host builder <see cref="Apply"/> was called on.</param>
    /// <returns>The remembered state, or a state without a path when <see cref="Apply"/> did not run.</returns>
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
/// What the environment variable <c>SMARTHAL_CONFIG_FILE</c> pointed at when the configuration was
/// built (FR-20).
/// </summary>
/// <param name="FilePath">The configured path, or <see langword="null"/> when the variable is unset.</param>
/// <param name="Exists">Whether a file exists at <paramref name="FilePath"/>.</param>
internal sealed record ExternalConfigurationFile(string? FilePath, bool Exists)
{
    /// <summary>The state for a process that was started without the environment variable.</summary>
    public static ExternalConfigurationFile NotConfigured { get; } = new(null, false);

    /// <summary>
    /// Inspects the configured path before the configuration is built.
    /// </summary>
    /// <param name="filePath">The raw value of <c>SMARTHAL_CONFIG_FILE</c>.</param>
    /// <returns>The resolved state.</returns>
    /// <exception cref="InvalidDataException">
    /// <paramref name="filePath"/> names a directory, which no JSON source can read.
    /// </exception>
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
