using System.Collections.ObjectModel;
using SmartHal.Contracts.Api;
using SmartHal.Contracts.Schema;

namespace SmartHal.Core.Catalog;

/// <summary>
/// Provides the first catalog of core capabilities: 25 capability types in five groups.
/// </summary>
/// <remarks>
/// <para>
/// The groups are <see cref="SystemCapabilities"/>, <see cref="ActuatorCapabilities"/>,
/// <see cref="SensorCapabilities"/>, <see cref="EnergyCapabilities"/> and <see cref="IiotCapabilities"/>. Rare
/// quantities start as vendor capabilities and move into the core catalog when needed.
/// </para>
/// <para>
/// Every property and method builds a new object graph on each call. The graphs hold no shared mutable state - their
/// collections are read-only at every depth and their JSON values belong to that graph alone - so callers may attach
/// or change what they receive without affecting anyone else.
/// </para>
/// </remarks>
public static class CoreCapabilityCatalog
{
    /// <summary>
    /// Gets every core capability type in catalog order: system, actuators, sensors, energy, IIoT.
    /// </summary>
    /// <value>The 25 core capability types.</value>
    /// <remarks>Every access returns a new, independent instance, so no caller can affect another.</remarks>
    public static IReadOnlyList<CapabilityType> All =>
    [
        .. SystemCapabilities.All,
        .. ActuatorCapabilities.All,
        .. SensorCapabilities.All,
        .. EnergyCapabilities.All,
        .. IiotCapabilities.All
    ];

    /// <summary>
    /// Creates a type catalog that holds the core capability types and the reusable core data types.
    /// </summary>
    /// <returns>
    /// A type catalog with <see cref="All"/> as its capabilities, <see cref="CoreDataTypes.Hsv"/> as its only data
    /// type, and no channel profiles, device types or migrations. Every call returns a new, independent instance.
    /// </returns>
    public static TypeCatalog ToTypeCatalog() =>
        new TypeCatalog(All, ReadOnlyCollection<ChannelProfile>.Empty, ReadOnlyCollection<DeviceType>.Empty, [CoreDataTypes.Hsv]);
}
