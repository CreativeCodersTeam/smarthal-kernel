using SmartHal.Contracts.Api;
using SmartHal.Contracts.DataTypes;
using SmartHal.Contracts.Schema;
using SmartHal.Contracts.Topology;
using SmartHal.Core.Abstractions.Validation;

namespace SmartHal.Core.Validation;

/// <summary>
/// Checks schema types and instances against the structural rules R1 to R15 of the contracts.
/// </summary>
/// <remarks>
/// <para>
/// The validator is stateless: it keeps nothing between calls and has no dependencies, so one instance can be shared
/// by any number of threads and registered as a singleton.
/// </para>
/// <para>
/// Every call reports all violations it finds in traversal order. Paths are relative to the validated object in
/// camelCase JSON notation; for a catalog they start with the list of the entry, for example
/// <c>capabilities[3].alarms.highLimit.source.event</c>.
/// </para>
/// <para>
/// A <see langword="null"/> value where the contract requires one - an entry of a list or map, a mandatory list or
/// map itself, or a mandatory name - is reported once as <see cref="ValidationCodes.NullEntry"/> at its own path.
/// It never produces a second code: rules that would need the missing value are skipped, and a key whose value is
/// <see langword="null"/> still counts as declared.
/// </para>
/// <para>
/// The struct depth of rule R1 follows <see cref="RefType"/> references wherever a catalog is at hand, that is in
/// <see cref="Validate(TypeCatalog)"/>: a reference counts the struct levels of the data type it points to, and a
/// reference that leads back to the data type definition it is part of is reported as
/// <see cref="ValidationCodes.RefCycle"/>. A reference that merely points to a cyclic data type is not a cycle of its
/// own and counts no level for the cyclic part; the cycle is reported at its members. Every data type is expanded once
/// per run, so the cost grows linearly with the catalog. The overloads without a catalog cannot resolve references and
/// measure the depth within one data type only. A reference that cannot be resolved is reported as
/// <see cref="ValidationCodes.UnresolvedType"/> and adds no level.
/// </para>
/// <para>
/// Patterns follow ECMA-262, the dialect of JSON Schema. The ECMA-262-only constructs the .NET parser does not know
/// - the code point escape <c>\u{…}</c>, the empty class <c>[]</c> and the negated empty class <c>[^]</c> - are
/// rewritten into .NET equivalents first; the pattern is then compiled in the ECMAScript mode of .NET. That rejects
/// malformed expressions, but the mode does not restrict the syntax, so some .NET-only constructs still pass: atomic
/// groups <c>(?&gt;…)</c>, inline options <c>(?i)</c>, character class subtraction <c>[a-z-[aeiou]]</c>, the anchors
/// <c>\A</c> and <c>\Z</c> and comments <c>(?#…)</c>. Lookbehind and Unicode property escapes are ECMA-262 and pass
/// rightly.
/// </para>
/// <para>
/// Messages format numbers with the invariant culture, so they read the same in every environment.
/// </para>
/// </remarks>
public sealed class ContractValidator : IContractValidator
{
    /// <inheritdoc/>
    public IReadOnlyList<ValidationError> Validate(CapabilityType capabilityType)
    {
        ArgumentNullException.ThrowIfNull(capabilityType);

        return Run(context => CapabilityTypeRules.Check(capabilityType, string.Empty, context, index: null));
    }

    /// <inheritdoc/>
    public IReadOnlyList<ValidationError> Validate(DataTypeDef dataTypeDef)
    {
        ArgumentNullException.ThrowIfNull(dataTypeDef);

        return Run(context => SchemaRules.Check(dataTypeDef, string.Empty, context, index: null));
    }

    /// <inheritdoc/>
    public IReadOnlyList<ValidationError> Validate(ChannelProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);

        return Run(context => SchemaRules.Check(profile, string.Empty, context, index: null));
    }

    /// <inheritdoc/>
    public IReadOnlyList<ValidationError> Validate(DeviceType deviceType)
    {
        ArgumentNullException.ThrowIfNull(deviceType);

        return Run(context => SchemaRules.Check(deviceType, string.Empty, context, index: null));
    }

    /// <inheritdoc/>
    public IReadOnlyList<ValidationError> Validate(CapabilityMigration migration)
    {
        ArgumentNullException.ThrowIfNull(migration);

        return Run(context => SchemaRules.Check(migration, string.Empty, context, index: null));
    }

    /// <inheritdoc/>
    public IReadOnlyList<ValidationError> Validate(Device device)
    {
        ArgumentNullException.ThrowIfNull(device);

        return Run(context => DeviceRules.Check(device, context, index: null));
    }

    /// <inheritdoc/>
    public IReadOnlyList<ValidationError> Validate(TypeCatalog catalog) => ValidateCatalog(catalog, observeIndex: null);

    /// <inheritdoc/>
    public IReadOnlyList<ValidationError> Validate(Device device, TypeCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(device);
        ArgumentNullException.ThrowIfNull(catalog);

        return Run(context => DeviceRules.Check(device, context, new CatalogIndex(catalog, judgeMissingLists: true)));
    }

    /// <summary>
    /// Checks every entry of a type catalog and lets the caller observe the index the run resolves references in.
    /// </summary>
    /// <remarks>
    /// The observer receives the index before the entries are checked; after the run it still holds the reference
    /// analysis of exactly this run, so a test can prove how often the run expanded each data type.
    /// </remarks>
    /// <param name="catalog">The type catalog to check.</param>
    /// <param name="observeIndex">The callback that receives the index of this run; <see langword="null"/> for none.</param>
    /// <returns>The violations found; empty when the catalog is valid.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="catalog"/> is <see langword="null"/>.</exception>
    internal static IReadOnlyList<ValidationError> ValidateCatalog(TypeCatalog catalog, Action<CatalogIndex>? observeIndex)
    {
        ArgumentNullException.ThrowIfNull(catalog);

        return Run(context => CatalogRules.Check(catalog, context, observeIndex));
    }

    private static IReadOnlyList<ValidationError> Run(Action<ValidationContext> check)
    {
        var context = new ValidationContext();

        check(context);

        return context.Errors;
    }
}
