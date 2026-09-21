using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;

namespace SmartHal.Server.Configuration;

/// <summary>
/// One validation violation, broken down into the section, the field and the reason the abort
/// message names (FR-24).
/// </summary>
/// <param name="Section">The configuration section the field belongs to, for example <c>SmartHal</c>.</param>
/// <param name="Field">The name of the field that breaks a rule, or an empty string when the
/// underlying message names none.</param>
/// <param name="Reason">The rule that was broken, in plain words.</param>
/// <remarks>
/// The record has room for these three parts and for nothing else. Slice 0 knows no way to mark a
/// field as a secret, so no configured value is carried along at all (G-9, NFR-4).
/// </remarks>
public sealed partial record OptionsFailure(string Section, string Field, string Reason)
{
    private const string OptionsTypeSuffix = "Options";

    /// <summary>
    /// Breaks the failure messages of a validation exception down into single violations.
    /// </summary>
    /// <param name="exception">The exception the options validation threw at start.</param>
    /// <returns>One entry per violation, in the order the validators reported them.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="exception"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// <para>
    /// Two message forms occur. <see cref="SmartHalOptionsValidator"/> writes
    /// <c>&lt;Section&gt;:&lt;Field&gt;: &lt;Reason&gt;</c>, and DataAnnotations writes
    /// <c>DataAnnotation validation failed for '…' members: 'X' with the error: '…'.</c>, which may
    /// name several members at once and then yields one entry per member.
    /// </para>
    /// <para>
    /// The DataAnnotations form carries no section, so it is taken from the name of the options type
    /// without its <c>Options</c> suffix - the same name the type declares as its section.
    /// </para>
    /// </remarks>
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
