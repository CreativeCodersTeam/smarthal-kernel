using System.Text.Json;
using System.Text.Json.Serialization;
using SmartHal.Contracts.Primitives;

namespace SmartHal.Contracts.Serialization;

/// <summary>
/// Converts a <see cref="TypeRef"/> to and from its JSON string form <c>"&lt;name&gt;@&lt;major&gt;"</c>.
/// </summary>
public sealed class TypeRefConverter : JsonConverter<TypeRef>
{
    /// <inheritdoc/>
    /// <exception cref="JsonException">The current token is not a string, or the string is not a valid type reference.</exception>
    public override TypeRef Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.String)
        {
            throw new JsonException($"A type reference must be a JSON string, but the token is {reader.TokenType}.");
        }

        var text = reader.GetString();

        return TypeRef.TryParse(text, out var value)
            ? value
            : throw new JsonException($"'{text}' is not a valid type reference; expected '<name>@<major>'.");
    }

    /// <inheritdoc/>
    /// <exception cref="JsonException">
    /// <paramref name="value"/> has no valid text form, for example a default instance or one built with
    /// parts that <see cref="TypeRef.Parse(string)"/> would reject.
    /// </exception>
    public override void Write(Utf8JsonWriter writer, TypeRef value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);

        // A value built directly (or left at its default) bypasses the parser; writing it would produce JSON
        // that no reader accepts, so the text form has to parse back to the same value.
        var text = value.ToString();

        if (!TypeRef.TryParse(text, out var parsed) || parsed != value)
        {
            throw new JsonException($"'{text}' is not a valid type reference and cannot be written.");
        }

        writer.WriteStringValue(text);
    }
}
