using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text.Json.Serialization;
using SmartHal.Contracts.Serialization;

namespace SmartHal.Contracts.Primitives;

/// <summary>
/// Represents the <c>Major.Minor</c> version a schema type carries, for example <c>2.1</c>.
/// </summary>
/// <param name="Major">The major version; it changes on incompatible changes.</param>
/// <param name="Minor">The minor version; it changes on additive changes.</param>
[JsonConverter(typeof(TypeVersionConverter))]
public readonly record struct TypeVersion(int Major, int Minor) : IParsable<TypeVersion>
{
    /// <summary>
    /// Returns the canonical text form of the version.
    /// </summary>
    /// <returns>The version in the form <c>&lt;major&gt;.&lt;minor&gt;</c>, for example <c>1.2</c>.</returns>
    public override string ToString() => string.Create(CultureInfo.InvariantCulture, $"{Major}.{Minor}");

    /// <summary>
    /// Converts the text form <c>&lt;major&gt;.&lt;minor&gt;</c> to a <see cref="TypeVersion"/>.
    /// </summary>
    /// <param name="s">A string that contains the version to convert.</param>
    /// <returns>The version that <paramref name="s"/> describes.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="s"/> is <see langword="null"/>.</exception>
    /// <exception cref="FormatException"><paramref name="s"/> is not a valid type version.</exception>
    public static TypeVersion Parse(string s)
    {
        ArgumentNullException.ThrowIfNull(s);

        return TryParse(s, out var result)
            ? result
            : throw new FormatException($"'{s}' is not a valid type version; expected '<major>.<minor>', for example '1.2'.");
    }

    /// <summary>
    /// Tries to convert the text form <c>&lt;major&gt;.&lt;minor&gt;</c> to a <see cref="TypeVersion"/>.
    /// </summary>
    /// <param name="s">A string that contains the version to convert.</param>
    /// <param name="result">The parsed version, or the default value on failure.</param>
    /// <returns><see langword="true"/> if <paramref name="s"/> was converted successfully; otherwise, <see langword="false"/>.</returns>
    public static bool TryParse([NotNullWhen(true)] string? s, out TypeVersion result)
    {
        result = default;

        if (s is null)
        {
            return false;
        }

        var separator = s.IndexOf('.', StringComparison.Ordinal);

        if (separator <= 0
            || !int.TryParse(s.AsSpan(0, separator), NumberStyles.None, CultureInfo.InvariantCulture, out var major)
            || !int.TryParse(s.AsSpan(separator + 1), NumberStyles.None, CultureInfo.InvariantCulture, out var minor))
        {
            return false;
        }

        result = new TypeVersion(major, minor);

        return true;
    }

    /// <inheritdoc cref="Parse(string)"/>
    static TypeVersion IParsable<TypeVersion>.Parse(string s, IFormatProvider? provider) => Parse(s);

    /// <inheritdoc cref="TryParse(string?, out TypeVersion)"/>
    static bool IParsable<TypeVersion>.TryParse([NotNullWhen(true)] string? s, IFormatProvider? provider, out TypeVersion result) =>
        TryParse(s, out result);
}
