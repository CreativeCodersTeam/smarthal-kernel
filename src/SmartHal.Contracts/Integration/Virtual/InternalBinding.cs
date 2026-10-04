using SmartHal.Contracts.Addressing;
using SmartHal.Contracts.Primitives;

namespace SmartHal.Contracts.Integration.Virtual;

/// <summary>
/// Describes the internal logic of a virtual device: which member capabilities it aggregates and how it fans
/// commands out to them.
/// </summary>
/// <param name="Members">The capabilities of the member devices.</param>
/// <param name="Aggregations">The aggregations that override the property defaults, keyed by property name; <see langword="null"/> when
/// none do.</param>
/// <param name="Substitutions">The substitute calls for members without a matching command; <see langword="null"/> when there are
/// none.</param>
public sealed record InternalBinding(
    IReadOnlyList<CapabilityAddress> Members,
    IReadOnlyDictionary<string, Aggregation>? Aggregations = null,
    IReadOnlyList<Substitution>? Substitutions = null);
