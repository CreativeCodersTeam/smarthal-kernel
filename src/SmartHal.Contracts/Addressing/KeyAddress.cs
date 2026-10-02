namespace SmartHal.Contracts.Addressing;

/// <summary>
/// Addresses an element of a capability by the readable keys of its device, channel and capability.
/// </summary>
/// <remarks>
/// A key may be a former key that is still valid as an alias; the kernel resolves it when it is called.
/// </remarks>
/// <param name="Device">The globally unique key of the device, for example <c>halle2.pumpe3</c>.</param>
/// <param name="Channel">The key of the channel within the device, for example <c>hydraulik</c>.</param>
/// <param name="Capability">The key of the capability within the channel, for example <c>druckseite</c>.</param>
/// <param name="Element">The name of the element within the capability, for example <c>value</c>.</param>
public sealed record KeyAddress(string Device, string Channel, string Capability, string Element) : ElementRef;
