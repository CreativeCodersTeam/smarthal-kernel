using System.Text.Json.Serialization;

namespace SmartHal.Contracts.Addressing;

/// <summary>
/// Addresses an element of a capability: a property, a command, an event or an alarm.
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "by")]
[JsonDerivedType(typeof(Address), "id")]
[JsonDerivedType(typeof(KeyAddress), "key")]
public abstract record ElementRef;
