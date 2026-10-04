using System.Text.Json.Serialization;

namespace SmartHal.Contracts.Schema;

/// <summary>
/// Describes what raises an alarm: the device itself or a platform rule.
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "kind")]
[JsonDerivedType(typeof(DeviceAlarmSource), "device")]
[JsonDerivedType(typeof(RuleAlarmSource), "rule")]
public abstract record AlarmSource;
