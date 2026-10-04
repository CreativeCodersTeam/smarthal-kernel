using SmartHal.Contracts.Primitives;

namespace SmartHal.Contracts.Integration.Virtual;

/// <summary>
/// Chooses a member call by the value of an enumerated parameter.
/// </summary>
/// <param name="Command">The fanned-out command the substitution applies to.</param>
/// <param name="MemberType">The capability type of the members the substitution serves.</param>
/// <param name="Param">The parameter of the command whose value selects the call.</param>
/// <param name="Map">The call for every parameter value; a value without an entry skips the member.</param>
public sealed record EnumMapSubstitution(
    string Command,
    TypeRef MemberType,
    string Param,
    IReadOnlyDictionary<string, MemberCall> Map) : Substitution(Command, MemberType);
