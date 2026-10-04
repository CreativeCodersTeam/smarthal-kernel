using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace SmartHal.Contracts.Serialization;

/// <summary>
/// Provides the serializer options every SmartHal contract is written and read with.
/// </summary>
public static class ContractsJson
{
    /// <summary>
    /// Gets the shared, read-only serializer options for SmartHal contracts.
    /// </summary>
    public static JsonSerializerOptions Options { get; } = CreateOptions();

    private static JsonSerializerOptions CreateOptions()
    {
        var resolver = new DefaultJsonTypeInfoResolver();
        resolver.Modifiers.Add(RejectMissingDiscriminator);

        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            TypeInfoResolver = resolver,
            AllowOutOfOrderMetadataProperties = true,
            AllowDuplicateProperties = false,
            RespectNullableAnnotations = true,
            RespectRequiredConstructorParameters = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            Converters =
            {
                new StrictEnumConverterFactory(),
                new IsoDurationConverter()
            }
        };

        options.MakeReadOnly();

        return options;
    }

    // Without a discriminator System.Text.Json falls back to the abstract base type itself and throws a
    // NotSupportedException; an abstract type without polymorphism cannot be created at all. Creating an abstract type
    // is exactly that case, so it is turned into the JsonException every other malformed input produces.
    private static void RejectMissingDiscriminator(JsonTypeInfo typeInfo)
    {
        if (typeInfo.Kind != JsonTypeInfoKind.Object || !typeInfo.Type.IsAbstract)
        {
            return;
        }

        var typeName = typeInfo.Type.Name;
        var message = typeInfo.PolymorphismOptions is { } polymorphism
            ? $"A {typeName} needs its type discriminator '{polymorphism.TypeDiscriminatorPropertyName}' to be read."
            : $"The abstract type {typeName} cannot be read directly; read one of its concrete types instead.";

        typeInfo.CreateObject = () => throw new JsonException(message);
    }
}
