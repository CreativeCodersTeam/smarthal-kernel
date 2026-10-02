using SmartHal.Contracts.Primitives;

namespace SmartHal.Contracts.Integration;

/// <summary>
/// Refers to the binding template of a device type for one protocol.
/// </summary>
/// <param name="DeviceType">The device type that carries the template.</param>
/// <param name="Protocol">The protocol of the template; see <see cref="Protocols"/> for the known values.</param>
public sealed record TemplateRef(TypeRef DeviceType, string Protocol);
