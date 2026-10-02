using System.Text.Json;
using System.Text.Json.Serialization;
using SmartHal.Contracts.Primitives;

namespace SmartHal.Contracts.Serialization;

/// <summary>
/// Converts a <see cref="TypeVersion"/> to and from its JSON string form <c>"&lt;major&gt;.&lt;minor&gt;"</c>.
/// </summary>
public sealed class TypeVersionConverter : JsonConverter<TypeVersion>
{
    /// <inheritdoc/>
    /// <exception cref="JsonException">The current token is not a string, or the string is not a valid type version.</exception>
    public override TypeVersion Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.String)
        {
            throw new JsonException($"A type version must be a JSON string, but the token is {reader.TokenType}.");
        }

        var text = reader.GetString();

        return TypeVersion.TryParse(text, out var value)
            ? value
            : throw new JsonException($"'{text}' is not a valid type version; expected '<major>.<minor>'.");
    }

    /// <inheritdoc/>
    /// <exception cref="JsonException">
    /// <paramref name="value"/> has no valid text form, for example one built with a negative part that
    /// <see cref="TypeVersion.Parse(string)"/> would reject.
    /// </exception>
    public override void Write(Utf8JsonWriter writer, TypeVersion value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);

        // A value built directly bypasses the parser; writing it would produce JSON
        // that no reader accepts, so the text form has to parse back to the same value.
        var text = value.ToString();

        if (!TypeVersion.TryParse(text, out var parsed) || parsed != value)
        {
            throw new JsonException($"'{text}' is not a valid type version and cannot be written.");
        }

        writer.WriteStringValue(text);
    }
}
