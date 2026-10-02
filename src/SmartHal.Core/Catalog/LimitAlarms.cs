using System.Text.Json.Nodes;
using SmartHal.Contracts.DataTypes;
using SmartHal.Contracts.Primitives;
using SmartHal.Contracts.Schema;

namespace SmartHal.Core.Catalog;

/// <summary>
/// Creates the limit alarms every measured quantity of the core catalog carries.
/// </summary>
/// <remarks>
/// Each quantity gets <c>highLimit</c> (condition above) and <c>lowLimit</c> (condition below) with the parameters
/// <c>limit</c>, <c>delay</c> and <c>hysteresis</c>. The limit has no default: the alarm stays inactive until the
/// capability instance sets it, for example 8 bar for one particular pump.
/// </remarks>
internal static class LimitAlarms
{
    /// <summary>The name of the alarm that fires above the limit.</summary>
    public const string High = "highLimit";

    /// <summary>The name of the alarm that fires below the limit.</summary>
    public const string Low = "lowLimit";

    /// <summary>The default delay before a limit violation raises the alarm.</summary>
    public static readonly TimeSpan DefaultDelay = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Creates the high and the low limit alarm of a measured property.
    /// </summary>
    /// <param name="property">The name of the measured property.</param>
    /// <param name="unit">The UCUM unit of the property, which the limit and the hysteresis share.</param>
    /// <param name="severity">The severity of both alarms.</param>
    /// <returns>The two alarm definitions, keyed by alarm name.</returns>
    public static IReadOnlyDictionary<string, AlarmDef> For(string property, string unit, Severity severity) =>
        CatalogDefinitions.Map(
            (High, Create(property, unit, severity, AlarmCondition.Above, "The value is above its high limit.")),
            (Low, Create(property, unit, severity, AlarmCondition.Below, "The value is below its low limit.")));

    /// <summary>
    /// Creates the parameters of a rule alarm on a measured property.
    /// </summary>
    /// <param name="unit">The UCUM unit of the limit and the hysteresis.</param>
    /// <param name="limit">The default limit; <see langword="null"/> when the instance has to set it.</param>
    /// <param name="delay">The default delay.</param>
    /// <param name="hysteresis">The default hysteresis.</param>
    /// <returns>The parameters <c>limit</c>, <c>delay</c> and <c>hysteresis</c>.</returns>
    public static IReadOnlyDictionary<string, AlarmParameter> Parameters(string unit, double? limit, TimeSpan delay, double hysteresis)
    {
        var quantity = new NumberType(unit);

        return CatalogDefinitions.Map(
            ("limit", new AlarmParameter(quantity, limit is null ? null : JsonValue.Create(limit.Value))),
            ("delay", new AlarmParameter(new DurationType(), JsonValue.Create(System.Xml.XmlConvert.ToString(delay)))),
            ("hysteresis", new AlarmParameter(quantity, JsonValue.Create(hysteresis))));
    }

    private static AlarmDef Create(string property, string unit, Severity severity, AlarmCondition condition, string message) =>
        new AlarmDef(severity, message, new RuleAlarmSource(property, condition),
            Parameters(unit, limit: null, DefaultDelay, hysteresis: 0));
}
