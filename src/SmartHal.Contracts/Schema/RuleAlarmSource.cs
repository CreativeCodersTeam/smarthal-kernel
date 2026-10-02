namespace SmartHal.Contracts.Schema;

/// <summary>
/// Describes an alarm the platform raises by evaluating a rule on one of the properties of the capability.
/// </summary>
/// <param name="Property">The name of the property the rule evaluates.</param>
/// <param name="Condition">The condition that raises the alarm.</param>
public sealed record RuleAlarmSource(string Property, AlarmCondition Condition) : AlarmSource;
