using System.Collections.Frozen;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SmartHal.Contracts.Serialization;

/// <summary>
/// Creates converters that serialize the contract enums strictly as snake_case names.
/// </summary>
internal sealed class StrictEnumConverterFactory : JsonConverterFactory
{
    /// <inheritdoc/>
    public override bool CanConvert(Type typeToConvert) =>
        typeToConvert.IsEnum && typeToConvert.Assembly == typeof(StrictEnumConverterFactory).Assembly;

    /// <inheritdoc/>
    public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options) =>
        (JsonConverter)Activator.CreateInstance(typeof(StrictEnumConverter<>).MakeGenericType(typeToConvert))!;

    private sealed class StrictEnumConverter<TEnum> : JsonConverter<TEnum>
        where TEnum : struct, Enum
    {
        private static readonly FrozenDictionary<string, TEnum> ValuesByName = Enum.GetValues<TEnum>()
            .ToFrozenDictionary(NameOf, StringComparer.Ordinal);

        private static readonly FrozenDictionary<TEnum, string> NamesByValue = Enum.GetValues<TEnum>()
            .ToFrozenDictionary(value => value, NameOf);

        // In declaration order, for the error message; the frozen dictionaries do not keep any order.
        private static readonly string ExpectedNames = string.Join(", ", Enum.GetValues<TEnum>().Select(NameOf));

        public override TEnum Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.String)
            {
                throw new JsonException(
                    $"A value of {typeof(TEnum).Name} must be a JSON string, but the token is {reader.TokenType}.");
            }

            return Parse(reader.GetString()!);
        }

        public override void Write(Utf8JsonWriter writer, TEnum value, JsonSerializerOptions options)
        {
            ArgumentNullException.ThrowIfNull(writer);

            writer.WriteStringValue(WrittenName(value));
        }

        // Dictionary keys follow the same rules as values.
        public override TEnum ReadAsPropertyName(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
            Parse(reader.GetString()!);

        public override void WriteAsPropertyName(Utf8JsonWriter writer, TEnum value, JsonSerializerOptions options)
        {
            ArgumentNullException.ThrowIfNull(writer);

            writer.WritePropertyName(WrittenName(value));
        }

        private static TEnum Parse(string text) =>
            ValuesByName.TryGetValue(text, out var value)
                ? value
                : throw new JsonException(
                    $"'{text}' is not a value of {typeof(TEnum).Name}; expected one of {ExpectedNames}.");

        private static string WrittenName(TEnum value) =>
            NamesByValue.TryGetValue(value, out var name)
                ? name
                : throw new JsonException($"'{value}' is not a declared value of {typeof(TEnum).Name} and cannot be written.");

        private static string NameOf(TEnum value) => JsonNamingPolicy.SnakeCaseLower.ConvertName(value.ToString());
    }
}
