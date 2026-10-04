using SmartHal.Contracts.Integration;
using SmartHal.Contracts.Primitives;

namespace SmartHal.Contracts.Schema;

/// <summary>
/// Defines a kind of device: manufacturer, model, the template of its channels and how it is bound.
/// </summary>
/// <param name="Name">The namespaced name of the device type, for example <c>acme.trv2</c>.</param>
/// <param name="Version">The version of the device type.</param>
/// <param name="Manufacturer">The manufacturer of the device.</param>
/// <param name="Model">The model of the device.</param>
/// <param name="Channels">The channels a device of this type has, including the root channel <c>0</c>.</param>
/// <param name="Sleepy">The wake-up behavior of a sleepy device; <see langword="null"/> when the device is always reachable.</param>
/// <param name="BindingTemplates">The binding templates, one per protocol; <see langword="null"/> when there are none.</param>
public sealed record DeviceType(
    string Name,
    TypeVersion Version,
    string Manufacturer,
    string Model,
    IReadOnlyList<ChannelTemplate> Channels,
    SleepyConfig? Sleepy = null,
    IReadOnlyList<BindingTemplate>? BindingTemplates = null);
