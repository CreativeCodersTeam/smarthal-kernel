using SmartHal.Contracts.DataTypes;

namespace SmartHal.Contracts.Integration;

/// <summary>
/// Describes how a device type is bound through one protocol: its parameters and all its mappings.
/// </summary>
/// <param name="Protocol">The protocol of the template; see <see cref="Protocols"/> for the known values.</param>
/// <param name="Parameters">The parameters a device has to supply, keyed by name, for example <c>slaveId</c>.</param>
/// <param name="Mappings">The mappings between protocol addresses and capability elements.</param>
public sealed record BindingTemplate(
    string Protocol,
    IReadOnlyDictionary<string, DataType> Parameters,
    IReadOnlyList<Mapping> Mappings);
