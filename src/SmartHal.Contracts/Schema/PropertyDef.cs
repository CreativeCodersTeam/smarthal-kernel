using SmartHal.Contracts.DataTypes;
using SmartHal.Contracts.Primitives;

namespace SmartHal.Contracts.Schema;

/// <summary>
/// Defines a property of a capability type.
/// </summary>
/// <param name="DataType">The data type of the value, including unit and constraints.</param>
/// <param name="Category">The role of the property.</param>
/// <param name="DeriveSetter"><see langword="true"/> to derive a setter command for a state property as well; <see langword="null"/> for
/// the default of the category.</param>
/// <param name="Feature">The feature flag the property depends on; <see langword="null"/> when it is always present.</param>
/// <param name="History">The default history policy, which an instance may override; <see langword="null"/> when the property is not
/// historized.</param>
/// <param name="Aggregation">The default aggregation for virtual devices; <see langword="null"/> when none applies.</param>
public sealed record PropertyDef(
    DataType DataType,
    PropertyCategory Category,
    bool? DeriveSetter = null,
    string? Feature = null,
    HistoryPolicy? History = null,
    Aggregation? Aggregation = null);
