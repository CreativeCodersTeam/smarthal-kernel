using System.Text.Json.Serialization;

namespace SmartHal.Contracts.Integration.Transforms;

/// <summary>
/// Describes one named, chainable function of a value conversion between device and capability.
/// </summary>
/// <remarks>
/// On reading a chain runs forwards, on writing backwards. The JSON form carries the discriminator <c>fn</c>:
/// <c>scale</c>, <c>offset</c>, <c>invert</c>, <c>reciprocal</c>, <c>log10</c> or <c>enumMap</c>.
/// </remarks>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "fn")]
[JsonDerivedType(typeof(ScaleStep), "scale")]
[JsonDerivedType(typeof(OffsetStep), "offset")]
[JsonDerivedType(typeof(InvertStep), "invert")]
[JsonDerivedType(typeof(ReciprocalStep), "reciprocal")]
[JsonDerivedType(typeof(Log10Step), "log10")]
[JsonDerivedType(typeof(EnumMapStep), "enumMap")]
public abstract record TransformStep;
