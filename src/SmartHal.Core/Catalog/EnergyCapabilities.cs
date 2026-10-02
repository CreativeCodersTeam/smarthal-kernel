using SmartHal.Contracts.DataTypes;
using SmartHal.Contracts.Primitives;
using SmartHal.Contracts.Schema;
using static SmartHal.Core.Catalog.CatalogDefinitions;

namespace SmartHal.Core.Catalog;

/// <summary>
/// Provides the energy capabilities of the core catalog.
/// </summary>
/// <remarks>Every property returns a new, independent instance on each access.</remarks>
public static class EnergyCapabilities
{
    /// <summary>
    /// Gets <c>core.power@1</c>: electrical power and energy.
    /// </summary>
    /// <value>
    /// The capability type with <c>activePower</c> and <c>energy</c>, the diagnostic <c>voltage</c> and
    /// <c>current</c>, and the command <c>resetEnergy()</c>.
    /// </value>
    /// <remarks>Every access returns a new, independent instance, so no caller can affect another.</remarks>
    public static CapabilityType Power => Capability(
        "core.power",
        Map(
            ("activePower", State(new NumberType(Units.Watt), Aggregation.Sum, HistoryPolicies.Measurement(1))),
            ("energy", State(new NumberType(Units.WattHour), Aggregation.Sum, HistoryPolicies.Measurement())),
            ("voltage", Diagnostic(new NumberType(Units.Volt), Aggregation.Avg, HistoryPolicies.Diagnostic)),
            ("current", Diagnostic(new NumberType(Units.Ampere), Aggregation.Sum, HistoryPolicies.Diagnostic))),
        Map(("resetEnergy", Ack(affects: ["energy"]))));

    /// <summary>
    /// Gets every energy capability in catalog order.
    /// </summary>
    /// <value>The one energy capability.</value>
    /// <remarks>Every access returns a new, independent instance, so no caller can affect another.</remarks>
    public static IReadOnlyList<CapabilityType> All => [Power];
}
