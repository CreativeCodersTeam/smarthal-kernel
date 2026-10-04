using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;

namespace SmartHal.Server.Configuration;

/// <summary>
/// Represents a single configuration validation violation, so it can be logged in a structured form.
/// </summary>
/// <param name="Section">The configuration section the field belongs to.</param>
/// <param name="Field">The name of the offending field, or an empty string when it is unknown.</param>
/// <param name="Reason">The rule that was broken.</param>
/// <remarks>
/// The configured value is deliberately not carried, because it may be a secret.
/// </remarks>
public sealed partial record OptionsFailure(string Section, string Field, string Reason)
{
    private const string OptionsTypeSuffix = "Options";

    /// <summary>
    /// Extracts the single violations from an options validation exception.
    /// </summary>
    /// <param name="exception">The exception the options validation threw.</param>
    /// <returns>The violations, in the order they were reported.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="exception"/> is <see langword="null"/>.</exception>
    public static IReadOnlyList<OptionsFailure> Parse(OptionsValidationException exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        var section = SectionOf(exception.OptionsType);
        var failures = new List<OptionsFailure>();

        foreach (var message in exception.Failures)
        {
            failures.AddRange(ParseMessage(message, section));
        }

        return failures;
    }

    private static IEnumerable<OptionsFailure> ParseMessage(string message, string section)
    {
        var dataAnnotations = DataAnnotationsMessage().Match(message);

        if (dataAnnotations.Success)
        {
            var reason = dataAnnotations.Groups["reason"].Value;

            return dataAnnotations.Groups["members"].Value
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(member => new OptionsFailure(section, member, reason));
        }

        var custom = CustomMessage().Match(message);

        return custom.Success
            ? [new OptionsFailure(custom.Groups["section"].Value, custom.Groups["field"].Value, custom.Groups["reason"].Value)]
            : [new OptionsFailure(section, "", message)];
    }

    private static string SectionOf(Type optionsType)
    {
        var name = optionsType.Name;

        return name.Length > OptionsTypeSuffix.Length
            && name.EndsWith(OptionsTypeSuffix, StringComparison.Ordinal)
                ? name[..^OptionsTypeSuffix.Length]
                : name;
    }

    [GeneratedRegex(
        @"^DataAnnotation validation failed for .*?members: '(?<members>[^']*)' with the error: '(?<reason>.*)'\.$",
        RegexOptions.Singleline | RegexOptions.CultureInvariant)]
    private static partial Regex DataAnnotationsMessage();

    [GeneratedRegex(
        @"^(?<section>[^\s:]+):(?<field>[^\s:]+):\s*(?<reason>.+)$",
        RegexOptions.Singleline | RegexOptions.CultureInvariant)]
    private static partial Regex CustomMessage();
}
