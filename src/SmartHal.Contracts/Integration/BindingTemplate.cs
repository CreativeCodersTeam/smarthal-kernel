using SmartHal.Contracts.DataTypes;

namespace SmartHal.Contracts.Integration;

/// <summary>
/// Describes how a device type is bound through one protocol: its parameters and all its mappings.
/// </summary>
/// <remarks>
/// <para>
/// The template hangs off the device type. Its mappings contain placeholders such as <c>${slaveId}</c> or
/// <c>${ieeeAddr}</c>, which the binding of a device fills from its parameters.
/// </para>
/// <para>
/// Equality compares list, dictionary and <see cref="System.Text.Json.Nodes.JsonNode"/> members by reference, not
/// by content; to compare contents, compare the JSON forms written with
/// <see cref="SmartHal.Contracts.Serialization.ContractsJson.Options"/>.
/// </para>
/// </remarks>
/// <param name="Protocol">The protocol of the template; see <see cref="Protocols"/> for the known values.</param>
/// <param name="Parameters">The parameters a device has to supply, keyed by name, for example <c>slaveId</c>.</param>
/// <param name="Mappings">The mappings between protocol addresses and capability elements.</param>
public sealed record BindingTemplate(
    string Protocol,
    IReadOnlyDictionary<string, DataType> Parameters,
    IReadOnlyList<Mapping> Mappings);
