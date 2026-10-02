using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace SmartHal.Contracts.Serialization;

/// <summary>
/// Provides the serializer options every SmartHal contract is written and read with.
/// </summary>
/// <remarks>
/// <para>
/// The options follow the web defaults (camelCase property names, case-insensitive property names on reading,
/// numbers also from strings) and add:
/// </para>
/// <list type="bullet">
/// <item><description>
/// enums as their exact snake_case names, for example <c>ActiveUnacked</c> as <c>"active_unacked"</c>; integers,
/// C# member names, other casings, comma-separated combinations and unknown names are rejected with a
/// <see cref="JsonException"/>;
/// </description></item>
/// <item><description>
/// <see cref="TimeSpan"/> values as non-negative ISO 8601 durations in days, hours, minutes and seconds, such as
/// <c>"PT30S"</c> or <c>"P90D"</c>;
/// </description></item>
/// <item><description>no <see langword="null"/> properties on writing;</description></item>
/// <item><description>type discriminators that need not be the first property of an object on reading;</description></item>
/// <item><description>
/// a JSON object for an abstract contract type without its type discriminator is rejected with a
/// <see cref="JsonException"/>;
/// </description></item>
/// <item><description>duplicate property names and duplicate dictionary keys are rejected with a <see
/// cref="JsonException"/>;</description></item>
/// <item><description>
/// strict reading of the nullability contract: a missing constructor parameter without a default value, or
/// <see langword="null"/> for a non-nullable member, is rejected with a <see cref="JsonException"/>.
/// </description></item>
/// </list>
/// <para>
/// Every reading error of the contract surfaces as a <see cref="JsonException"/>. System.Text.Json does not check
/// the elements of collections, so a <see langword="null"/> entry in a list or a map passes; the contract validator
/// of the kernel reports it.
/// </para>
/// <para>
/// The instance is read-only; a caller that needs different settings copies it with
/// <see cref="JsonSerializerOptions(JsonSerializerOptions)"/>.
/// </para>
/// </remarks>
public static class ContractsJson
{
    /// <summary>
    /// Gets the shared, read-only serializer options for SmartHal contracts.
    /// </summary>
    /// <value>An options instance that cannot be modified.</value>
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
