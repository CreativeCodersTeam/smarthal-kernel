namespace SmartHal.Contracts.Topology;

/// <summary>
/// Specifies the level of a location in the hierarchy site, building, floor, room or zone.
/// </summary>
public enum LocationKind
{
    /// <summary>A site, the top of the hierarchy.</summary>
    Site = 0,

    /// <summary>A building on a site.</summary>
    Building = 1,

    /// <summary>A floor of a building.</summary>
    Floor = 2,

    /// <summary>A room on a floor.</summary>
    Room = 3,

    /// <summary>A zone, for example an area of a hall that is not a room of its own.</summary>
    Zone = 4
}
