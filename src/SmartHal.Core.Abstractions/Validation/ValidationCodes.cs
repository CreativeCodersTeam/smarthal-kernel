namespace SmartHal.Core.Abstractions.Validation;

/// <summary>
/// Provides the codes a <see cref="ValidationError"/> carries, one per violated rule.
/// </summary>
public static class ValidationCodes
{
    /// <summary>A collection holds a <see langword="null"/> entry where the contract requires a value.</summary>
    public const string NullEntry = "null_entry";

    /// <summary>A struct is nested deeper than two levels (R1).</summary>
    public const string StructTooDeep = "struct_too_deep";

    /// <summary>A struct names a required field it does not define (R2).</summary>
    public const string UnknownField = "unknown_field";

    /// <summary>An enum data type has no values (R2).</summary>
    public const string EmptyEnum = "empty_enum";

    /// <summary>An enum data type lists a value twice (R2).</summary>
    public const string DuplicateEnumValue = "duplicate_enum_value";

    /// <summary>
    /// A bound, count or limit is out of range: a numeric data type has a minimum above its maximum or a bound that is
    /// not finite, a profile capability has a negative count or a minimum count above its maximum count, a string or
    /// array data type has a negative length limit, or a deadband is negative (R2, R7).
    /// </summary>
    public const string InvalidRange = "invalid_range";

    /// <summary>A numeric data type has a step that is not greater than zero (R2).</summary>
    public const string InvalidStep = "invalid_step";

    /// <summary>A command with completion mode result defines no result type (R3).</summary>
    public const string MissingResult = "missing_result";

    /// <summary>A command with completion mode confirmed names no affected property (R3).</summary>
    public const string MissingAffects = "missing_affects";

    /// <summary>A reference names a property the capability type does not define (R4, R5, R15).</summary>
    public const string UnknownProperty = "unknown_property";

    /// <summary>A command names a required parameter it does not define (R4).</summary>
    public const string UnknownParameter = "unknown_parameter";

    /// <summary>A device alarm names an event the capability type does not define (R5).</summary>
    public const string UnknownEvent = "unknown_event";

    /// <summary>A feature flag is not declared by the capability type (R6, R12).</summary>
    public const string UnknownFeature = "unknown_feature";

    /// <summary>A device or device type has no root channel <c>0</c>, or more than one (R8).</summary>
    public const string RootChannel = "root_channel";

    /// <summary>A key occurs twice within its scope (R8).</summary>
    public const string DuplicateKey = "duplicate_key";

    /// <summary>A device that is not virtual has no device type (R9).</summary>
    public const string MissingDeviceType = "missing_device_type";

    /// <summary>A migration does not lead from one major version of a type to a higher one of the same type (R10).</summary>
    public const string InvalidMigration = "invalid_migration";

    /// <summary>A type reference cannot be resolved in the type catalog (R11).</summary>
    public const string UnresolvedType = "unresolved_type";

    /// <summary>A capability implements a version that does not match its type reference or the catalog (R12).</summary>
    public const string VersionMismatch = "version_mismatch";

    /// <summary>A device lacks a channel or capability its device type prescribes (R13).</summary>
    public const string TemplateMismatch = "template_mismatch";

    /// <summary>A channel lacks a capability its profile requires, or carries it too few or too many times (R14).</summary>
    public const string ProfileViolation = "profile_violation";

    /// <summary>An instance override names an alarm the capability type does not define (R15).</summary>
    public const string UnknownAlarm = "unknown_alarm";

    /// <summary>An instance override names a parameter the alarm does not define (R15).</summary>
    public const string UnknownAlarmParameter = "unknown_alarm_parameter";

    /// <summary>A reference to a reusable data type leads back to the data type definition it is part of (R1).</summary>
    public const string RefCycle = "ref_cycle";

    /// <summary>A string data type carries a pattern that is not a valid regular expression.</summary>
    public const string InvalidPattern = "invalid_pattern";

    /// <summary>A timeout, interval or retention is not greater than zero, or a minimum interval is negative.</summary>
    public const string InvalidDuration = "invalid_duration";
}
