using SmartHal.Contracts.Primitives;

namespace SmartHal.Contracts.DataTypes;

/// <summary>
/// Refers to a reusable data type that is defined once as a <see cref="DataTypeDef"/>.
/// </summary>
/// <param name="Ref">The reference to the reusable type, for example <c>core.types.hsv@1</c>.</param>
public sealed record RefType(TypeRef Ref) : DataType;
