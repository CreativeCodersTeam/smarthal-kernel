using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text.Json.Serialization;
using SmartHal.Contracts.Serialization;

namespace SmartHal.Contracts.Primitives;

/// <summary>
/// Refers to a schema type by its name and major version, for example <c>core.pressure@1</c>.
/// </summary>
/// <param name="Name">The namespaced name of the type, for example <c>core.pressure</c> or <c>vendor.acme.filter</c>.</param>
/// <param name="Major">The major version of the type.</param>
[JsonConverter(typeof(TypeRefConverter))]
public readonly record struct TypeRef(string Name, int Major) : IParsable<TypeRef>
{
    /// <summary>
    /// Returns the canonical text form of the reference.
    /// </summary>
    /// <returns>The reference in the form <c>&lt;name&gt;@&lt;major&gt;</c>, for example <c>core.pressure@1</c>.</returns>
    public override string ToString() => string.Create(CultureInfo.InvariantCulture, $"{Name}@{Major}");

    /// <summary>
    /// Converts the text form <c>&lt;name&gt;@&lt;major&gt;</c> to a <see cref="TypeRef"/>.
    /// </summary>
    /// <param name="s">A string that contains the reference to convert.</param>
    /// <returns>The reference that <paramref name="s"/> describes.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="s"/> is <see langword="null"/>.</exception>
    /// <exception cref="FormatException"><paramref name="s"/> is not a valid type reference.</exception>
    public static TypeRef Parse(string s)
    {
        ArgumentNullException.ThrowIfNull(s);

        return TryParse(s, out var result)
            ? result
            : throw new FormatException(
                $"'{s}' is not a valid type reference; expected '<name>@<major>', for example 'core.pressure@1'.");
    }

    /// <summary>
    /// Tries to convert the text form <c>&lt;name&gt;@&lt;major&gt;</c> to a <see cref="TypeRef"/>.
    /// </summary>
    /// <param name="s">A string that contains the reference to convert.</param>
    /// <param name="result">The parsed reference, or the default value on failure.</param>
    /// <returns><see langword="true"/> if <paramref name="s"/> was converted successfully; otherwise, <see langword="false"/>.</returns>
    public static bool TryParse([NotNullWhen(true)] string? s, out TypeRef result)
    {
        result = default;

        if (s is null)
        {
            return false;
        }

        var separator = s.IndexOf('@', StringComparison.Ordinal);

        if (separator <= 0 || separator != s.LastIndexOf('@'))
        {
            return false;
        }

        var name = s[..separator];

        if (name.Any(char.IsWhiteSpace)
            || !int.TryParse(s.AsSpan(separator + 1), NumberStyles.None, CultureInfo.InvariantCulture, out var major))
        {
            return false;
        }

        result = new TypeRef(name, major);

        return true;
    }

    /// <inheritdoc cref="Parse(string)"/>
    static TypeRef IParsable<TypeRef>.Parse(string s, IFormatProvider? provider) => Parse(s);

    /// <inheritdoc cref="TryParse(string?, out TypeRef)"/>
    static bool IParsable<TypeRef>.TryParse([NotNullWhen(true)] string? s, IFormatProvider? provider, out TypeRef result) =>
        TryParse(s, out result);
}
