using System.Text.Json.Serialization;
using SmartHal.Contracts.Primitives;

namespace SmartHal.Contracts.Integration.Virtual;

/// <summary>
/// Describes how a fanned-out command reaches a member that cannot execute it directly.
/// </summary>
/// <remarks>
/// Substitutions are fixed declarative patterns instead of an expression language; more complex logic belongs to the
/// automation layer. The JSON form carries the discriminator <c>pattern</c>: <c>threshold</c>, <c>enumMap</c> or
/// <c>fixed</c>.
/// </remarks>
/// <param name="Command">The fanned-out command the substitution applies to, for example <c>setLevel</c>.</param>
/// <param name="MemberType">The capability type of the members the substitution serves, for example <c>core.onoff@1</c>.</param>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "pattern")]
[JsonDerivedType(typeof(ThresholdSubstitution), "threshold")]
[JsonDerivedType(typeof(EnumMapSubstitution), "enumMap")]
[JsonDerivedType(typeof(FixedSubstitution), "fixed")]
public abstract record Substitution(string Command, TypeRef MemberType);
