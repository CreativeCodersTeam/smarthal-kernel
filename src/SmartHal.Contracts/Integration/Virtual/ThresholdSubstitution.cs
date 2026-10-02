using SmartHal.Contracts.Primitives;

namespace SmartHal.Contracts.Integration.Virtual;

/// <summary>
/// Chooses between two member calls by comparing a numeric parameter with a threshold.
/// </summary>
/// <remarks>
/// For example, <c>setLevel</c> with <c>level</c> above 0 becomes <c>on</c>, otherwise <c>off</c>.
/// </remarks>
/// <param name="Command">The fanned-out command the substitution applies to.</param>
/// <param name="MemberType">The capability type of the members the substitution serves.</param>
/// <param name="Param">The numeric parameter of the command that is compared.</param>
/// <param name="Above">The threshold; a parameter value strictly above it selects <paramref name="Then"/>.</param>
/// <param name="Then">The call used when the parameter is above the threshold.</param>
/// <param name="Else">The call used otherwise.</param>
public sealed record ThresholdSubstitution(
    string Command,
    TypeRef MemberType,
    string Param,
    double Above,
    MemberCall Then,
    MemberCall Else) : Substitution(Command, MemberType);
