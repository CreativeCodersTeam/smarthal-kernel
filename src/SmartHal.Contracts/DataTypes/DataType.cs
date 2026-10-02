using System.Text.Json.Serialization;

namespace SmartHal.Contracts.DataTypes;

/// <summary>
/// Describes the data type of a property, a command parameter, a command result or an event payload.
/// </summary>
/// <remarks>
/// <para>
/// The data types are a subset of JSON Schema, as in the W3C Web of Things. The JSON form carries the
/// discriminator <c>type</c>: <c>boolean</c>, <c>integer</c>, <c>number</c>, <c>string</c>, <c>enum</c>,
/// <c>timestamp</c>, <c>duration</c>, <c>object</c>, <c>array</c> or <c>ref</c>.
/// </para>
/// <para>
/// Only scalars and enums are historized directly; a struct is split into its fields for the history. Structs are
/// at most two levels deep so that the split and the bindings stay manageable.
/// </para>
/// <para>
/// The discriminator is written only when a value is serialized through this base type, which every contract
/// property does. Serializing a derived type through its own static type omits it.
/// </para>
/// </remarks>
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
