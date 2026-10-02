using System.Text.Json.Serialization;

namespace SmartHal.Contracts.Addressing;

/// <summary>
/// Addresses an element of a capability: a property, a command, an event or an alarm.
/// </summary>
/// <remarks>
/// Every runtime object and every bus message is addressed the same way: device, channel, capability, element.
/// The address uses either UUIDs (<see cref="Address"/>) or readable keys (<see cref="KeyAddress"/>); the kernel
/// resolves keys when it is called. The JSON form carries the discriminator <c>by</c>: <c>id</c> or <c>key</c>.
/// </remarks>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "by")]
[JsonDerivedType(typeof(Address), "id")]
[JsonDerivedType(typeof(KeyAddress), "key")]
public abstract record ElementRef;
