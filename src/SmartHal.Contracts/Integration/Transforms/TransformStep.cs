using System.Text.Json.Serialization;

namespace SmartHal.Contracts.Integration.Transforms;

/// <summary>
/// Base type of the value conversions a mapping chains between device and capability.
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "fn")]
[JsonDerivedType(typeof(ScaleStep), "scale")]
[JsonDerivedType(typeof(OffsetStep), "offset")]
[JsonDerivedType(typeof(InvertStep), "invert")]
[JsonDerivedType(typeof(ReciprocalStep), "reciprocal")]
[JsonDerivedType(typeof(Log10Step), "log10")]
[JsonDerivedType(typeof(EnumMapStep), "enumMap")]
public abstract record TransformStep;
