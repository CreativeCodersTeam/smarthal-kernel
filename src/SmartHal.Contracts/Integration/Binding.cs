using System.Text.Json.Nodes;
using SmartHal.Contracts.Integration.Virtual;

namespace SmartHal.Contracts.Integration;

/// <summary>
/// Assigns a device to an adapter, or to internal logic for a virtual device.
/// </summary>
/// <param name="Id">The immutable id of the binding.</param>
/// <param name="DeviceId">The id of the bound device.</param>
/// <param name="Kind">Whether the binding is a protocol binding or an internal one.</param>
/// <param name="Parameters">The binding parameters that fill the template placeholders, for example <c>slaveId = 3</c>.</param>
/// <param name="Status">The reachability the binding reports.</param>
/// <param name="AdapterId">The id of the adapter; set for <see cref="BindingKind.Protocol"/> only.</param>
/// <param name="Template">The template the mappings come from; <see langword="null"/> when there is none.</param>
/// <param name="Overrides">The mappings that replace those of the template; <see langword="null"/> when there are none.</param>
/// <param name="Internal">The logic of a virtual device; set for <see cref="BindingKind.Internal"/> only.</param>
public sealed record Binding(
    Guid Id,
    Guid DeviceId,
    BindingKind Kind,
    IReadOnlyDictionary<string, JsonNode?> Parameters,
    BindingStatus Status,
    Guid? AdapterId = null,
    TemplateRef? Template = null,
    IReadOnlyList<Mapping>? Overrides = null,
    InternalBinding? Internal = null);
