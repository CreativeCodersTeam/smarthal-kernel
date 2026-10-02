namespace SmartHal.Contracts.Topology;

/// <summary>
/// Specifies the lifecycle stage of a device.
/// </summary>
public enum DeviceLifecycle
{
    /// <summary>The device was approved from the inbox and has not reported a valid message yet.</summary>
    Provisioned = 0,

    /// <summary>The device has reported its first valid message and is in operation.</summary>
    Active = 1,

    /// <summary>The device has been taken out of operation.</summary>
    Decommissioned = 2
}
