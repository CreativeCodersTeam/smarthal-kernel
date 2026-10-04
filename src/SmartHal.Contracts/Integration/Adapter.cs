namespace SmartHal.Contracts.Integration;

/// <summary>
/// Describes an adapter that connects devices of one protocol.
/// </summary>
/// <param name="Id">The immutable id of the adapter.</param>
/// <param name="Key">The readable key of the adapter.</param>
/// <param name="Protocol">The protocol the adapter speaks; see <see cref="Protocols"/> for the known values.</param>
/// <param name="Connections">The connections the adapter holds.</param>
/// <param name="Discovery">The discovery settings of the adapter.</param>
/// <param name="Status">The operating state of the adapter.</param>
public sealed record Adapter(
    Guid Id,
    string Key,
    string Protocol,
    IReadOnlyList<Connection> Connections,
    DiscoverySettings Discovery,
    AdapterStatus Status);
