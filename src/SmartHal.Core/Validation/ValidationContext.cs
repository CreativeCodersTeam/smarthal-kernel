using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using SmartHal.Core.Abstractions.Validation;

namespace SmartHal.Core.Validation;

/// <summary>
/// Collects the violations of one validation run and builds the paths they are reported at.
/// </summary>
/// <remarks>
/// Paths use the camelCase JSON notation of the contracts: members and dictionary keys are joined with a dot, list
/// entries carry their index in brackets, and the validated object itself has the empty path.
/// </remarks>
internal sealed class ValidationContext
{
    private readonly List<ValidationError> _errors = [];

    /// <summary>
    /// Gets the violations reported so far, in the order they were found.
    /// </summary>
    public IReadOnlyList<ValidationError> Errors => _errors;

    /// <summary>
    /// Reports a violation.
    /// </summary>
    /// <param name="path">The path of the violation.</param>
    /// <param name="code">The code of the violated rule.</param>
    /// <param name="message">The description of the violation.</param>
    public void Add(string path, string code, string message) => _errors.Add(new ValidationError(path, code, message));

    /// <summary>
    /// Appends a member name or a dictionary key to a path.
    /// </summary>
    /// <param name="path">The path of the enclosing object.</param>
    /// <param name="member">The member name or dictionary key.</param>
    /// <returns>The path of the member.</returns>
    public static string Member(string path, string member) => path.Length == 0 ? member : $"{path}.{member}";

    /// <summary>
    /// Appends a list index to a path.
    /// </summary>
    /// <param name="path">The path of the list.</param>
    /// <param name="index">The index of the entry.</param>
    /// <returns>The path of the list entry.</returns>
    public static string Index(string path, int index) => string.Create(CultureInfo.InvariantCulture, $"{path}[{index}]");

    /// <summary>
    /// Tests whether a value is missing although the contract types it as non-nullable.
    /// </summary>
    /// <param name="value">The value to test.</param>
    /// <returns><see langword="true"/> if <paramref name="value"/> is <see langword="null"/>; otherwise, <see langword="false"/>.</returns>
    /// <remarks>
    /// The serializer does not check the nullability of generic type arguments, so a list or dictionary entry can be
    /// <see langword="null"/> even where the contract says otherwise; the parameter type lets that check stand.
    /// </remarks>
    public static bool IsNull([NotNullWhen(false)] object? value) => value is null;

    /// <summary>
    /// Returns the entries of a list that are present and reports every <see langword="null"/> entry.
    /// </summary>
    /// <typeparam name="T">The type of the entries.</typeparam>
    /// <param name="list">The list; <see langword="null"/> is reported only when <paramref name="required"/> is set.</param>
    /// <param name="path">The path of the list.</param>
    /// <param name="required"><see langword="true"/> when the list itself is mandatory; otherwise, <see langword="false"/>.</param>
    /// <returns>The present entries together with their index in the list.</returns>
    public IReadOnlyList<(T Item, int Index)> Entries<T>(IReadOnlyList<T>? list, string path, bool required)
        where T : class
    {
        if (IsNull(list))
        {
            ReportMissingCollection(path, required);

            return [];
        }

        var present = new List<(T Item, int Index)>(list.Count);

        for (var i = 0; i < list.Count; i++)
        {
            var item = list[i];

            if (IsNull(item))
            {
                Add(Index(path, i), ValidationCodes.NullEntry, Invariant($"The entry at index {i} is null."));
            }
            else
            {
                present.Add((item, i));
            }
        }

        return present;
    }

    /// <summary>
    /// Returns the entries of a dictionary whose value is present and reports every <see langword="null"/> value.
    /// </summary>
    /// <typeparam name="T">The type of the values.</typeparam>
    /// <param name="map">The dictionary; <see langword="null"/> is reported only when <paramref name="required"/> is set.</param>
    /// <param name="path">The path of the dictionary.</param>
    /// <param name="required"><see langword="true"/> when the dictionary itself is mandatory; otherwise, <see langword="false"/>.</param>
    /// <returns>The present values together with their key.</returns>
    public IReadOnlyList<(T Item, string Key)> Entries<T>(IReadOnlyDictionary<string, T>? map, string path, bool required)
        where T : class
    {
        if (IsNull(map))
        {
            ReportMissingCollection(path, required);

            return [];
        }

        var present = new List<(T Item, string Key)>(map.Count);

        foreach (var (key, item) in map)
        {
            if (IsNull(item))
            {
                Add(Member(path, key), ValidationCodes.NullEntry, $"The entry '{key}' is null.");
            }
            else
            {
                present.Add((item, key));
            }
        }

        return present;
    }

    /// <summary>
    /// Formats a message with the invariant culture, so numbers read the same whatever culture the caller runs in.
    /// </summary>
    /// <param name="message">The message with its interpolated values.</param>
    /// <returns>The formatted message.</returns>
    public static string Invariant(FormattableString message) => message.ToString(CultureInfo.InvariantCulture);

    /// <summary>
    /// Returns every key of a dictionary, including the keys whose value is <see langword="null"/>.
    /// </summary>
    /// <typeparam name="T">The type of the values.</typeparam>
    /// <param name="map">The dictionary.</param>
    /// <returns>
    /// The declared keys, or <see langword="null"/> when the dictionary itself is missing, so that references into it
    /// are not judged against an absent declaration.
    /// </returns>
    public static HashSet<string>? KeysOf<T>(IReadOnlyDictionary<string, T>? map) =>
        IsNull(map) ? null : map.Keys.ToHashSet(StringComparer.Ordinal);

    private void ReportMissingCollection(string path, bool required)
    {
        if (required)
        {
            Add(path, ValidationCodes.NullEntry, "The mandatory collection is null.");
        }
    }
}
