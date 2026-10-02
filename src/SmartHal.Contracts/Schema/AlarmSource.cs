using System.Text.Json.Serialization;

namespace SmartHal.Contracts.Schema;

/// <summary>
/// Describes what raises an alarm: the device itself or a platform rule.
/// </summary>
/// <remarks>
/// The JSON form carries the discriminator <c>kind</c>: <c>device</c> or <c>rule</c>.
/// </remarks>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "kind")]
[JsonDerivedType(typeof(DeviceAlarmSource), "device")]
[JsonDerivedType(typeof(RuleAlarmSource), "rule")]
public abstract record AlarmSource;
