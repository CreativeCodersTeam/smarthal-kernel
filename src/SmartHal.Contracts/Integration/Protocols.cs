namespace SmartHal.Contracts.Integration;

/// <summary>
/// Provides the names of the known protocols.
/// </summary>
/// <remarks>
/// The list is open: a protocol is a plain string, so new adapters can add their own names.
/// </remarks>
public static class Protocols
{
    /// <summary>The Matter protocol.</summary>
    public const string Matter = "matter";

    /// <summary>The Zigbee protocol.</summary>
    public const string Zigbee = "zigbee";

    /// <summary>The Modbus protocol (TCP or RTU).</summary>
    public const string Modbus = "modbus";

    /// <summary>The OPC UA protocol.</summary>
    public const string OpcUa = "opcua";

    /// <summary>The MQTT protocol.</summary>
    public const string Mqtt = "mqtt";
}
