namespace SmartHal.Contracts.Addressing;

/// <summary>
/// Addresses a capability by the UUIDs of its device, channel and capability.
/// </summary>
/// <param name="DeviceId">The id of the device.</param>
/// <param name="ChannelId">The id of the channel within the device.</param>
/// <param name="CapabilityId">The id of the capability within the channel.</param>
public sealed record CapabilityAddress(Guid DeviceId, Guid ChannelId, Guid CapabilityId);
