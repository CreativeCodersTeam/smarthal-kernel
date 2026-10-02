using System.Text.Json.Serialization;

namespace SmartHal.Contracts.Bus;

/// <summary>
/// Describes a message on the bus that distributes every change.
/// </summary>
/// <remarks>
/// <para>
/// Delivery is at least once: no message is lost, but duplicates are possible. <see cref="Seq"/> rises per source -
/// property, command or event stream - so consumers discard duplicates and detect gaps. The order is guaranteed per
/// source, not across sources.
/// </para>
/// <para>
/// The JSON form carries the discriminator <c>kind</c>: <c>StateChanged</c>, <c>EventOccurred</c> or
/// <c>CommandUpdated</c>. How messages map onto a transport, for example MQTT topics, is defined outside the kernel.
/// </para>
/// </remarks>
/// <param name="Seq">The sequence number of the message within its source.</param>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "kind")]
[JsonDerivedType(typeof(StateChanged), "StateChanged")]
[JsonDerivedType(typeof(EventOccurred), "EventOccurred")]
[JsonDerivedType(typeof(CommandUpdated), "CommandUpdated")]
public abstract record BusMessage(long Seq);
