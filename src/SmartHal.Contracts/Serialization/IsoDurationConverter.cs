using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Xml;

namespace SmartHal.Contracts.Serialization;

/// <summary>
/// Converts a <see cref="TimeSpan"/> to and from an ISO 8601 duration such as <c>"PT30S"</c>.
/// </summary>
public sealed partial class IsoDurationConverter : JsonConverter<TimeSpan>
{
    /// <inheritdoc/>
    /// <exception cref="JsonException">The token is not a non-negative ISO 8601 duration.</exception>
    public override TimeSpan Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.String)
        {
            throw new JsonException($"A duration must be a JSON string, but the token is {reader.TokenType}.");
        }

        var text = reader.GetString()!;

        if (!DurationPattern().IsMatch(text))
        {
            throw new JsonException(
                $"'{text}' is not a non-negative ISO 8601 duration in days, hours, minutes and seconds such as 'PT30S'.");
        }

        try
        {
            return XmlConvert.ToTimeSpan(text);
        }
        catch (Exception exception) when (exception is FormatException or OverflowException)
        {
            // The pattern guarantees the form; XmlConvert still rejects values beyond the range of a TimeSpan.
            throw new JsonException($"'{text}' is outside the range of a duration.", exception);
        }
    }

    /// <inheritdoc/>
    /// <exception cref="JsonException"><paramref name="value"/> is negative.</exception>
    public override void Write(Utf8JsonWriter writer, TimeSpan value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);

        if (value < TimeSpan.Zero)
        {
            throw new JsonException($"The negative duration '{value}' cannot be written.");
        }

        writer.WriteStringValue(XmlConvert.ToString(value));
    }

    // P, then days and/or a time part with hours, minutes and seconds; at least one component. Seconds take at most
    // seven fractional digits, the resolution of a TimeSpan, so nothing is rounded silently. Only ASCII digits count,
    // and \z rather than $ keeps a trailing line feed out.
    [GeneratedRegex(
        @"\AP(?=[0-9]|T[0-9])([0-9]+D)?" + @"(T(?=[0-9])([0-9]+H)?([0-9]+M)?([0-9]+(\.[0-9]{1,7})?S)?)?\z",
        RegexOptions.CultureInvariant,
        1000)]
    private static partial Regex DurationPattern();
}
