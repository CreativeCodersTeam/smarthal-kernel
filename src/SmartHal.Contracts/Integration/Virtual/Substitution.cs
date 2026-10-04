using System.Text.Json.Serialization;
using SmartHal.Contracts.Primitives;

namespace SmartHal.Contracts.Integration.Virtual;

/// <summary>
/// Describes how a fanned-out command reaches a member that cannot execute it directly.
/// </summary>
/// <param name="Command">The fanned-out command the substitution applies to, for example <c>setLevel</c>.</param>
/// <param name="MemberType">The capability type of the members the substitution serves, for example <c>core.onoff@1</c>.</param>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "pattern")]
[JsonDerivedType(typeof(ThresholdSubstitution), "threshold")]
[JsonDerivedType(typeof(EnumMapSubstitution), "enumMap")]
[JsonDerivedType(typeof(FixedSubstitution), "fixed")]
public abstract record Substitution(string Command, TypeRef MemberType);
