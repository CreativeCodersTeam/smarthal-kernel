using SmartHal.Contracts.Primitives;

namespace SmartHal.Contracts.Integration.Virtual;

/// <summary>
/// Always replaces the fanned-out command with the same member call.
/// </summary>
/// <param name="Command">The fanned-out command the substitution applies to.</param>
/// <param name="MemberType">The capability type of the members the substitution serves.</param>
/// <param name="Use">The call used for every member of the given type.</param>
public sealed record FixedSubstitution(string Command, TypeRef MemberType, MemberCall Use) : Substitution(Command, MemberType);
