using System.Text.Json.Nodes;
using SmartHal.Contracts.DataTypes;

namespace SmartHal.Contracts.Schema;

/// <summary>
/// Defines a parameter of a rule alarm, for example the limit, the delay or the hysteresis.
/// </summary>
/// <param name="DataType">The data type of the parameter.</param>
/// <param name="Default">The default value; <see langword="null"/> when the instance has to set it before the alarm becomes active.</param>
public sealed record AlarmParameter(DataType DataType, JsonNode? Default = null);
