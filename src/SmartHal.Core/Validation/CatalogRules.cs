using SmartHal.Contracts.Api;
using SmartHal.Contracts.Primitives;
using SmartHal.Core.Abstractions.Validation;

namespace SmartHal.Core.Validation;

/// <summary>
/// Checks a whole type catalog: every entry on its own, no type twice, and every reference between the entries (R11,
/// R14).
/// </summary>
internal static class CatalogRules
{
    /// <summary>
    /// Checks a type catalog.
    /// </summary>
    /// <param name="catalog">The catalog to check.</param>
    /// <param name="context">The context the violations are reported to.</param>
    /// <param name="observeIndex">The callback that receives the index of this run; <see langword="null"/> for none.</param>
    public static void Check(TypeCatalog catalog, ValidationContext context, Action<CatalogIndex>? observeIndex = null)
    {
        // Missing mandatory lists are reported below as null entries; references into them are not judged again.
        var index = new CatalogIndex(catalog, judgeMissingLists: false);
        observeIndex?.Invoke(index);

        var dataTypes = context.Entries(catalog.DataTypes, "dataTypes", required: false);
        CheckUnique(dataTypes, type => (type.Name, type.Version.Major), "dataTypes", context);
        dataTypes.Each("dataTypes", (type, path) => SchemaRules.Check(type, path, context, index));

        var capabilities = context.Entries(catalog.Capabilities, "capabilities", required: true);
        CheckUnique(capabilities, type => (type.Name, type.Version.Major), "capabilities", context);
        capabilities.Each("capabilities", (type, path) => CapabilityTypeRules.Check(type, path, context, index));

        var profiles = context.Entries(catalog.Profiles, "profiles", required: true);
        CheckUnique(profiles, profile => (profile.Name, profile.Version.Major), "profiles", context);
        profiles.Each("profiles", (profile, path) => SchemaRules.Check(profile, path, context, index));

        var deviceTypes = context.Entries(catalog.DeviceTypes, "deviceTypes", required: true);
        CheckUnique(deviceTypes, type => (type.Name, type.Version.Major), "deviceTypes", context);
        deviceTypes.Each("deviceTypes", (type, path) => SchemaRules.Check(type, path, context, index));

        var migrations = context.Entries(catalog.Migrations, "migrations", required: false);
        migrations.Each("migrations", (migration, path) => SchemaRules.Check(migration, path, context, index));
    }

    private static void Each<T>(this IReadOnlyList<(T Item, int Index)> entries, string path, Action<T, string> check)
    {
        foreach (var (item, i) in entries)
        {
            check(item, ValidationContext.Index(path, i));
        }
    }

    // An entry without a name has no identity to collide on; its missing name is reported once, as a null entry.
    private static void CheckUnique<T>(
        IReadOnlyList<(T Item, int Index)> entries,
        Func<T, (string Name, int Major)> identity,
        string path,
        ValidationContext context)
    {
        var seen = new HashSet<(string Name, int Major)>();

        foreach (var (item, i) in entries)
        {
            var id = identity(item);

            if (!ValidationContext.IsNull(id.Name) && !seen.Add(id))
            {
                context.Add(
                    ValidationContext.Member(ValidationContext.Index(path, i), "name"),
                    ValidationCodes.DuplicateKey,
                    $"The type '{new TypeRef(id.Name, id.Major)}' occurs more than once.");
            }
        }
    }
}
