namespace SmartHal.Contracts.Integration;

/// <summary>
/// Describes an adapter: the single place that speaks one protocol, holds its connections and discovers devices.
/// </summary>
/// <remarks>
/// <para>
/// Examples are a Zigbee adapter, a Modbus adapter, a Matter adapter or a vendor cloud adapter. The adapter knows the
/// protocol but no semantics; a <see cref="Binding"/> assigns a device to it.
/// </para>
/// <para>
/// Equality compares list, dictionary and <see cref="System.Text.Json.Nodes.JsonNode"/> members by reference, not
/// by content; to compare contents, compare the JSON forms written with
/// <see cref="SmartHal.Contracts.Serialization.ContractsJson.Options"/>.
/// </para>
/// </remarks>
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
