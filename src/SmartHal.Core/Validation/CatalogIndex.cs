using SmartHal.Contracts.Api;
using SmartHal.Contracts.DataTypes;
using SmartHal.Contracts.Primitives;
using SmartHal.Contracts.Schema;

namespace SmartHal.Core.Validation;

/// <summary>
/// Resolves type references against a type catalog by name and major version.
/// </summary>
/// <remarks>
/// <para>
/// When a catalog holds several minor versions of the same major version, the highest minor version wins.
/// <see langword="null"/> entries of the catalog are skipped; reporting them is the job of the catalog validation.
/// </para>
/// <para>
/// A mandatory list of the catalog that is itself <see langword="null"/> resolves nothing. Whether a reference into it
/// is then unresolved or simply not judged depends on the caller: a device is checked against the catalog as it is,
/// while the catalog validation has already reported the missing list and must not add a violation per reference.
/// </para>
/// </remarks>
internal sealed class CatalogIndex
{
    private readonly Dictionary<(string Name, int Major), CapabilityType> _capabilities;
    private readonly Dictionary<(string Name, int Major), ChannelProfile> _profiles;
    private readonly Dictionary<(string Name, int Major), DeviceType> _deviceTypes;
    private readonly Dictionary<(string Name, int Major), DataTypeDef> _dataTypes;
    private ReferenceGraph? _references;

    /// <summary>
    /// Initializes a new instance of the <see cref="CatalogIndex"/> class.
    /// </summary>
    /// <param name="catalog">The catalog to index.</param>
    /// <param name="judgeMissingLists">
    /// <see langword="true"/> to treat a missing mandatory list as empty, so references into it are unresolved;
    /// <see langword="false"/> to leave references into it unjudged.
    /// </param>
    public CatalogIndex(TypeCatalog catalog, bool judgeMissingLists)
    {
        CapabilitiesUnknown = !judgeMissingLists && ValidationContext.IsNull(catalog.Capabilities);
        ProfilesUnknown = !judgeMissingLists && ValidationContext.IsNull(catalog.Profiles);

        _capabilities = Build(catalog.Capabilities, type => type.Name, type => type.Version);
        _profiles = Build(catalog.Profiles, profile => profile.Name, profile => profile.Version);
        _deviceTypes = Build(catalog.DeviceTypes, type => type.Name, type => type.Version);
        _dataTypes = Build(catalog.DataTypes, type => type.Name, type => type.Version);
    }

    /// <summary>
    /// Gets a value that indicates whether references to capability types are left unjudged because the catalog has
    /// no capability list.
    /// </summary>
    /// <value><see langword="true"/> if the capability list is missing and not judged; otherwise, <see langword="false"/>.</value>
    public bool CapabilitiesUnknown { get; }

    /// <summary>
    /// Gets a value that indicates whether references to channel profiles are left unjudged because the catalog has no
    /// profile list.
    /// </summary>
    /// <value><see langword="true"/> if the profile list is missing and not judged; otherwise, <see langword="false"/>.</value>
    public bool ProfilesUnknown { get; }

    /// <summary>
    /// Tests whether a reference to a capability type has to be reported as unresolved.
    /// </summary>
    /// <param name="reference">The reference to resolve.</param>
    /// <returns><see langword="true"/> if the reference is judged and not found; otherwise, <see langword="false"/>.</returns>
    public bool IsUnresolvedCapability(TypeRef reference) => !CapabilitiesUnknown && FindCapability(reference) is null;

    /// <summary>
    /// Finds the capability type a reference points to.
    /// </summary>
    /// <param name="reference">The reference to resolve.</param>
    /// <returns>The capability type, or <see langword="null"/> when the catalog does not contain it.</returns>
    public CapabilityType? FindCapability(TypeRef reference) => _capabilities.GetValueOrDefault((reference.Name, reference.Major));

    /// <summary>
    /// Finds the channel profile a reference points to.
    /// </summary>
    /// <param name="reference">The reference to resolve.</param>
    /// <returns>The channel profile, or <see langword="null"/> when the catalog does not contain it.</returns>
    public ChannelProfile? FindProfile(TypeRef reference) => _profiles.GetValueOrDefault((reference.Name, reference.Major));

    /// <summary>
    /// Finds the device type a reference points to.
    /// </summary>
    /// <param name="reference">The reference to resolve.</param>
    /// <returns>The device type, or <see langword="null"/> when the catalog does not contain it.</returns>
    public DeviceType? FindDeviceType(TypeRef reference) => _deviceTypes.GetValueOrDefault((reference.Name, reference.Major));

    /// <summary>
    /// Gets the analysis of the references between the reusable data types of the catalog.
    /// </summary>
    /// <value>A graph built on first use and shared by every check of this validation run.</value>
    public ReferenceGraph References => _references ??= new ReferenceGraph(_dataTypes);

    /// <summary>
    /// Finds the reusable data type a reference points to.
    /// </summary>
    /// <param name="reference">The reference to resolve.</param>
    /// <returns>The data type definition, or <see langword="null"/> when the catalog does not contain it.</returns>
    public DataTypeDef? FindDataType(TypeRef reference) => _dataTypes.GetValueOrDefault((reference.Name, reference.Major));

    private static Dictionary<(string Name, int Major), T> Build<T>(
        IReadOnlyList<T>? entries,
        Func<T, string> name,
        Func<T, TypeVersion> version)
        where T : class
    {
        var index = new Dictionary<(string Name, int Major), T>();

        if (ValidationContext.IsNull(entries))
        {
            return index;
        }

        foreach (var entry in entries)
        {
            if (ValidationContext.IsNull(entry) || ValidationContext.IsNull(name(entry)))
            {
                continue;
            }

            var key = (name(entry), version(entry).Major);

            if (!index.TryGetValue(key, out var existing) || version(existing).Minor < version(entry).Minor)
            {
                index[key] = entry;
            }
        }

        return index;
    }
}
