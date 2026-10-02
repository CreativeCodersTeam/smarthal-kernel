namespace SmartHal.Contracts.Integration;

/// <summary>
/// Describes one connection of an adapter, for example a broker, a serial port or an OPC UA session.
/// </summary>
/// <param name="Id">The immutable id of the connection.</param>
/// <param name="AdapterId">The id of the adapter that holds the connection.</param>
/// <param name="Endpoint">The protocol-specific endpoint, for example a broker URL, a serial port or an OPC UA endpoint.</param>
/// <param name="Status">The state of the connection.</param>
public sealed record Connection(Guid Id, Guid AdapterId, string Endpoint, ConnectionStatus Status);
