using SmartHal.Contracts.Primitives;

namespace SmartHal.Contracts.Topology;

/// <summary>
/// Describes a place in the location hierarchy: site, building, floor, room or zone.
/// </summary>
/// <param name="Id">The immutable id of the location.</param>
/// <param name="Key">The readable key of the location.</param>
/// <param name="Name">The display name of the location.</param>
/// <param name="Kind">The level of the location in the hierarchy.</param>
/// <param name="ParentId">The id of the enclosing location; <see langword="null"/> for a top-level location.</param>
/// <param name="Aliases">The former keys, valid for a limited time; <see langword="null"/> when there are none.</param>
/// <param name="Tags">Free key/value tags the kernel does not check; <see langword="null"/> when there are none.</param>
public sealed record Location(
    Guid Id,
    string Key,
    string Name,
    LocationKind Kind,
    Guid? ParentId = null,
    IReadOnlyList<string>? Aliases = null,
    IReadOnlyDictionary<string, string>? Tags = null) : IIdentified;
