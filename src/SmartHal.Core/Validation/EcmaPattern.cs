using System.Globalization;
using System.Text;

namespace SmartHal.Core.Validation;

/// <summary>
/// Rewrites the ECMA-262 constructs the .NET regular expression parser does not know into .NET equivalents, so a
/// JSON Schema pattern can be checked for well-formedness with the .NET parser.
/// </summary>
/// <remarks>
/// <para>
/// The rewrite only serves the syntax check; the result is never matched against input. Patterns are read as in the
/// Unicode mode of ECMA-262, where a code point beyond the Basic Multilingual Plane is one character. The rewrite
/// handles:
/// </para>
/// <list type="bullet">
/// <item><description>
/// the code point escape <c>\u{…}</c> with any number of hexadecimal digits up to the value <c>10FFFF</c>, surrogates
/// excluded, which becomes a <c>\uXXXX</c> escape or a surrogate pair of them;
/// </description></item>
/// <item><description>the empty class <c>[]</c>, which matches nothing and becomes <c>(?!)</c>;</description></item>
/// <item><description>the negated empty class <c>[^]</c>, which matches any character and becomes <c>[\s\S]</c>.</description></item>
/// </list>
/// <para>
/// Inside a character class the .NET parser would see a code point beyond the Basic Multilingual Plane as two
/// characters and misread a range between two of them. Such a range is therefore checked here, on the code points,
/// and handed to the parser as a single neutral character; a lone code point beyond the plane is handed over the same
/// way. Ranges between characters of the plane are left to the parser.
/// </para>
/// <para>
/// Escaped characters are copied unchanged, so <c>\[]</c>, <c>\]</c> or <c>\\u{41}</c> are not rewritten.
/// </para>
/// </remarks>
internal static class EcmaPattern
{
    private const int MaxCodePoint = 0x10FFFF;

    private const int FirstAstralCodePoint = 0x10000;

    // Stands in for a range or a code point the parser would misread; any ordinary character will do.
    private const char NeutralCharacter = 'a';

    private const string NeutralText = "a";

    /// <summary>
    /// Rewrites a pattern for the .NET parser.
    /// </summary>
    /// <param name="pattern">The ECMA-262 pattern.</param>
    /// <returns>
    /// The pattern with the ECMA-262-only constructs rewritten, or <see langword="null"/> when it contains a malformed
    /// code point escape, a code point beyond <c>10FFFF</c> or a surrogate, or a reversed range involving a code point
    /// beyond the Basic Multilingual Plane.
    /// </returns>
    public static string? ToDotNet(string pattern)
    {
        var result = new StringBuilder(pattern.Length);
        var i = 0;

        while (i < pattern.Length)
        {
            var consumed = pattern[i] switch
            {
                '\\' => AppendEscape(pattern, i, result),
                '[' => AppendClass(pattern, i, result),
                _ => AppendLiteral(pattern[i], result)
            };

            if (consumed == 0)
            {
                return null;
            }

            i += consumed;
        }

        return result.ToString();
    }

    // Returns how many characters the escape at the position consumed; 0 for a malformed code point escape.
    private static int AppendEscape(string pattern, int position, StringBuilder result)
    {
        if (IsCodePointEscape(pattern, position))
        {
            if (!TryReadCodePoint(pattern, position, out var codePoint, out var length))
            {
                return 0;
            }

            foreach (var unit in char.ConvertFromUtf32(codePoint))
            {
                result.Append(CultureInfo.InvariantCulture, $"\\u{(int)unit:X4}");
            }

            return length;
        }

        // A plain escape is copied as a whole, so the escaped character is never interpreted here. A trailing
        // backslash is copied alone and left to the parser to reject.
        var plainLength = Math.Min(2, pattern.Length - position);
        result.Append(pattern, position, plainLength);

        return plainLength;
    }

    private static int AppendClass(string pattern, int start, StringBuilder result)
    {
        if (string.CompareOrdinal(pattern, start, "[]", 0, 2) == 0)
        {
            result.Append("(?!)");

            return 2;
        }

        if (string.CompareOrdinal(pattern, start, "[^]", 0, 3) == 0)
        {
            result.Append(@"[\s\S]");

            return 3;
        }

        var position = start + 1;
        var negated = position < pattern.Length && pattern[position] == '^';

        if (negated)
        {
            position++;
        }

        var atoms = new List<ClassAtom>();

        while (position < pattern.Length && pattern[position] != ']')
        {
            if (!TryReadAtom(pattern, position, out var atom))
            {
                return 0;
            }

            atoms.Add(atom);
            position += atom.Length;
        }

        if (position >= pattern.Length)
        {
            // An unterminated class is handed over as it is; the parser rejects it.
            result.Append(pattern, start, pattern.Length - start);

            return pattern.Length - start;
        }

        result.Append(negated ? "[^" : "[");

        if (!AppendAtoms(atoms, result))
        {
            return 0;
        }

        result.Append(']');

        return position + 1 - start;
    }

    // Writes the atoms of a class; false when a range involving a code point beyond the plane is reversed.
    private static bool AppendAtoms(List<ClassAtom> atoms, StringBuilder result)
    {
        var k = 0;

        while (k < atoms.Count)
        {
            if (k + 2 < atoms.Count && atoms[k + 1].IsHyphen)
            {
                if (!AppendRange(atoms[k], atoms[k + 2], result))
                {
                    return false;
                }

                k += 3;
            }
            else
            {
                result.Append(atoms[k].IsAstral ? NeutralText : atoms[k].Text);
                k++;
            }
        }

        return true;
    }

    private static bool AppendRange(ClassAtom from, ClassAtom to, StringBuilder result)
    {
        if (from.IsAstral || to.IsAstral)
        {
            if (from.CodePoint is { } low && to.CodePoint is { } high)
            {
                if (low > high)
                {
                    return false;
                }

                result.Append(NeutralCharacter);

                return true;
            }

            // A range between a code point and a class escape such as \d: the parser judges the shape.
            result.Append(from.IsAstral ? NeutralText : from.Text)
                .Append('-')
                .Append(to.IsAstral ? NeutralText : to.Text);

            return true;
        }

        result.Append(from.Text).Append('-').Append(to.Text);

        return true;
    }

    private static bool TryReadAtom(string pattern, int position, out ClassAtom atom)
    {
        var character = pattern[position];

        if (character == '\\')
        {
            return TryReadEscapeAtom(pattern, position, out atom);
        }

        if (char.IsHighSurrogate(character) && position + 1 < pattern.Length && char.IsLowSurrogate(pattern[position + 1]))
        {
            var codePoint = char.ConvertToUtf32(character, pattern[position + 1]);
            atom = new ClassAtom(pattern.Substring(position, 2), codePoint, IsHyphen: false);

            return true;
        }

        atom = new ClassAtom(character.ToString(), character, IsHyphen: character == '-');

        return true;
    }

    private static bool TryReadEscapeAtom(string pattern, int position, out ClassAtom atom)
    {
        if (IsCodePointEscape(pattern, position))
        {
            if (!TryReadCodePoint(pattern, position, out var codePoint, out var length))
            {
                atom = default;

                return false;
            }

            var text = codePoint < FirstAstralCodePoint
                ? string.Create(CultureInfo.InvariantCulture, $"\\u{codePoint:X4}")
                : NeutralText;
            atom = new ClassAtom(text, codePoint, IsHyphen: false, Length: length);

            return true;
        }

        if (TryReadUnicodeEscape(pattern, position, out var unit)
            && char.IsHighSurrogate((char)unit)
            && TryReadUnicodeEscape(pattern, position + 6, out var lowUnit)
            && char.IsLowSurrogate((char)lowUnit))
        {
            // 😀 is one code point in the Unicode mode.
            var codePoint = char.ConvertToUtf32((char)unit, (char)lowUnit);
            atom = new ClassAtom(pattern.Substring(position, 12), codePoint, IsHyphen: false);

            return true;
        }

        var escapeLength = Math.Min(2, pattern.Length - position);
        var escapeText = pattern.Substring(position, escapeLength);
        atom = new ClassAtom(escapeText, EscapedCodePoint(pattern, position, escapeLength), IsHyphen: false);

        return true;
    }

    // The code point a simple escape stands for, or null for a class escape such as \d or a trailing backslash.
    private static int? EscapedCodePoint(string pattern, int position, int escapeLength)
    {
        if (escapeLength < 2)
        {
            return null;
        }

        if (TryReadUnicodeEscape(pattern, position, out var unit))
        {
            return unit;
        }

        return pattern[position + 1] switch
        {
            'n' => '\n',
            'r' => '\r',
            't' => '\t',
            'f' => '\f',
            'v' => '\v',
            var escaped when char.IsAsciiLetterOrDigit(escaped) => null,
            var escaped => escaped
        };
    }

    private static bool IsCodePointEscape(string pattern, int position) =>
        position + 2 < pattern.Length && pattern[position + 1] == 'u' && pattern[position + 2] == '{';

    // Reads \u{…}: one or more hexadecimal digits, any number of leading zeros, at most 10FFFF, no surrogate.
    private static bool TryReadCodePoint(string pattern, int position, out int codePoint, out int length)
    {
        codePoint = 0;
        length = 0;

        var digitsStart = position + 3;
        var close = pattern.IndexOf('}', digitsStart);

        if (close <= digitsStart)
        {
            return false;
        }

        for (var i = digitsStart; i < close; i++)
        {
            var digit = HexValue(pattern[i]);

            if (digit < 0)
            {
                return false;
            }

            codePoint = (codePoint * 16) + digit;

            // Stopping here also keeps an arbitrarily long run of digits from overflowing.
            if (codePoint > MaxCodePoint)
            {
                return false;
            }
        }

        if (codePoint is >= 0xD800 and <= 0xDFFF)
        {
            return false;
        }

        length = close - position + 1;

        return true;
    }

    // Reads \uXXXX with exactly four hexadecimal digits.
    private static bool TryReadUnicodeEscape(string pattern, int position, out int unit)
    {
        unit = 0;

        if (position + 6 > pattern.Length || pattern[position] != '\\' || pattern[position + 1] != 'u')
        {
            return false;
        }

        for (var i = position + 2; i < position + 6; i++)
        {
            var digit = HexValue(pattern[i]);

            if (digit < 0)
            {
                return false;
            }

            unit = (unit * 16) + digit;
        }

        return true;
    }

    private static int HexValue(char character) => character switch
    {
        >= '0' and <= '9' => character - '0',
        >= 'a' and <= 'f' => character - 'a' + 10,
        >= 'A' and <= 'F' => character - 'A' + 10,
        _ => -1
    };

    private static int AppendLiteral(char character, StringBuilder result)
    {
        result.Append(character);

        return 1;
    }

    // One element of a character class: its text for the parser, the code point it stands for (null for a class
    // escape such as \d), whether it is an unescaped hyphen, and how many pattern characters it spans.
    private readonly record struct ClassAtom(string Text, int? CodePoint, bool IsHyphen, int Length = 0)
    {
        public int Length { get; } = Length > 0 ? Length : Text.Length;

        public bool IsAstral => CodePoint >= FirstAstralCodePoint;
    }
}
