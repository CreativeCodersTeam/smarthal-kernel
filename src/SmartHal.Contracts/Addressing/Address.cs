using System.Text.Json.Serialization;

namespace SmartHal.Contracts.Addressing;

/// <summary>
/// Addresses an element of a capability by the UUIDs of its device, channel and capability.
/// </summary>
/// <param name="DeviceId">The id of the device.</param>
/// <param name="ChannelId">The id of the channel within the device.</param>
/// <param name="CapabilityId">The id of the capability within the channel.</param>
/// <param name="Element">The name of the element within the capability, for example <c>value</c> or <c>setLevel</c>.</param>
public sealed record Address(Guid DeviceId, Guid ChannelId, Guid CapabilityId, string Element) : ElementRef
{
    /// <summary>
    /// Gets the address of the capability that owns the element.
    /// </summary>
    /// <value>The same device, channel and capability ids without the element; not serialized.</value>
    [JsonIgnore]
    public CapabilityAddress Capability => new(DeviceId, ChannelId, CapabilityId);
}
