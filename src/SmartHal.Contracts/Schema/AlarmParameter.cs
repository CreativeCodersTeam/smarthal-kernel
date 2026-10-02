using System.Text.Json.Nodes;
using SmartHal.Contracts.DataTypes;

namespace SmartHal.Contracts.Schema;

/// <summary>
/// Defines a parameter of a rule alarm, for example the limit, the delay or the hysteresis.
/// </summary>
/// <remarks>
/// <para>
/// The alarm definition sets the default; the capability instance sets the concrete value, for example 8 bar for one
/// particular pump. The value is changed through a command, like a config property.
/// </para>
/// <para>
/// Equality compares list, dictionary and <see cref="System.Text.Json.Nodes.JsonNode"/> members by reference, not
/// by content; to compare contents, compare the JSON forms written with
/// <see cref="SmartHal.Contracts.Serialization.ContractsJson.Options"/>.
/// </para>
/// </remarks>
/// <param name="DataType">The data type of the parameter.</param>
/// <param name="Default">The default value; <see langword="null"/> when the instance has to set it before the alarm becomes active.</param>
public sealed record AlarmParameter(DataType DataType, JsonNode? Default = null);
