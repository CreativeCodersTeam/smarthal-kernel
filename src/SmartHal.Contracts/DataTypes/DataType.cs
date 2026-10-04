using System.Text.Json.Serialization;

namespace SmartHal.Contracts.DataTypes;

/// <summary>
/// Describes the data type of a property, a command parameter, a command result or an event payload.
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(BooleanType), "boolean")]
[JsonDerivedType(typeof(IntegerType), "integer")]
[JsonDerivedType(typeof(NumberType), "number")]
[JsonDerivedType(typeof(StringType), "string")]
[JsonDerivedType(typeof(EnumType), "enum")]
[JsonDerivedType(typeof(TimestampType), "timestamp")]
[JsonDerivedType(typeof(DurationType), "duration")]
[JsonDerivedType(typeof(ObjectType), "object")]
[JsonDerivedType(typeof(ArrayType), "array")]
[JsonDerivedType(typeof(RefType), "ref")]
public abstract record DataType;
