using SmartHal.Contracts.Primitives;

namespace SmartHal.Contracts.Schema;

/// <summary>
/// Defines a capability a channel profile expects.
/// </summary>
/// <param name="Type">The expected capability type.</param>
/// <param name="Required"><see langword="true"/> when a channel with the profile must carry the capability; otherwise, <see
/// langword="false"/>.</param>
/// <param name="Min">The smallest number of instances of the type; <see langword="null"/> when not limited.</param>
/// <param name="Max">The largest number of instances of the type, for example 2 for suction and discharge pressure; <see langword="null"/>
/// when not limited.</param>
public sealed record ProfileCapability(TypeRef Type, bool Required, int? Min = null, int? Max = null);
