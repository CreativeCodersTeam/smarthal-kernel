using SmartHal.Contracts.Api;
using SmartHal.Contracts.DataTypes;
using SmartHal.Contracts.Schema;
using SmartHal.Contracts.Topology;

namespace SmartHal.Core.Abstractions.Validation;

/// <summary>
/// Checks schema types and instances against the structural rules the contracts document but do not enforce.
/// </summary>
/// <remarks>
/// <para>
/// The overloads without a <see cref="TypeCatalog"/> check one object on its own: struct depth, data type
/// constraints, commands, alarm sources, features, profile counts, keys and root channel, device type and
/// migrations (rules R1 to R10). The overloads with a catalog additionally resolve every type reference and check the
/// object against the types it refers to (rules R11 to R15).
/// </para>
/// <para>
/// A validator reports every violation it finds instead of stopping at the first one, and never throws for invalid
/// content: an empty list means the object is valid. <see langword="null"/> entries in collections, which the
/// serializer lets through, are reported as <see cref="ValidationCodes.NullEntry"/>.
/// </para>
/// </remarks>
public interface IContractValidator
{
    /// <summary>
    /// Checks a capability type on its own.
    /// </summary>
    /// <param name="capabilityType">The capability type to check.</param>
    /// <returns>The violations found; empty when the capability type is valid.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="capabilityType"/> is <see langword="null"/>.</exception>
    IReadOnlyList<ValidationError> Validate(CapabilityType capabilityType);

    /// <summary>
    /// Checks a reusable data type definition on its own.
    /// </summary>
    /// <param name="dataTypeDef">The data type definition to check.</param>
    /// <returns>The violations found; empty when the definition is valid.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="dataTypeDef"/> is <see langword="null"/>.</exception>
    IReadOnlyList<ValidationError> Validate(DataTypeDef dataTypeDef);

    /// <summary>
    /// Checks a channel profile on its own.
    /// </summary>
    /// <param name="profile">The channel profile to check.</param>
    /// <returns>The violations found; empty when the profile is valid.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="profile"/> is <see langword="null"/>.</exception>
    IReadOnlyList<ValidationError> Validate(ChannelProfile profile);

    /// <summary>
    /// Checks a device type on its own.
    /// </summary>
    /// <param name="deviceType">The device type to check.</param>
    /// <returns>The violations found; empty when the device type is valid.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="deviceType"/> is <see langword="null"/>.</exception>
    IReadOnlyList<ValidationError> Validate(DeviceType deviceType);

    /// <summary>
    /// Checks a capability migration on its own.
    /// </summary>
    /// <param name="migration">The migration to check.</param>
    /// <returns>The violations found; empty when the migration is valid.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="migration"/> is <see langword="null"/>.</exception>
    IReadOnlyList<ValidationError> Validate(CapabilityMigration migration);

    /// <summary>
    /// Checks a device with its channels and capabilities on its own.
    /// </summary>
    /// <param name="device">The device to check.</param>
    /// <returns>The violations found; empty when the device is valid.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="device"/> is <see langword="null"/>.</exception>
    IReadOnlyList<ValidationError> Validate(Device device);

    /// <summary>
    /// Checks every entry of a type catalog on its own and resolves the type references between the entries.
    /// </summary>
    /// <param name="catalog">The type catalog to check.</param>
    /// <returns>The violations found; empty when the catalog is valid.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="catalog"/> is <see langword="null"/>.</exception>
    IReadOnlyList<ValidationError> Validate(TypeCatalog catalog);

    /// <summary>
    /// Checks a device on its own and against the types it refers to.
    /// </summary>
    /// <param name="device">The device to check.</param>
    /// <param name="catalog">The type catalog the references of the device are resolved in.</param>
    /// <returns>The violations found; empty when the device is valid.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="device"/> or <paramref name="catalog"/> is <see
    /// langword="null"/>.</exception>
    IReadOnlyList<ValidationError> Validate(Device device, TypeCatalog catalog);
}
