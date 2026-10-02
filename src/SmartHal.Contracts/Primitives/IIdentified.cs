namespace SmartHal.Contracts.Primitives;

/// <summary>
/// Defines the identity every addressable instance object carries: an immutable id and a readable key.
/// </summary>
/// <remarks>
/// All internal references use <see cref="Id"/>. Topics and APIs may use <see cref="Key"/> instead; when a key is
/// renamed, the former key stays valid for a limited time as an alias. Locations, devices, channels and
/// capabilities implement this interface.
/// </remarks>
public interface IIdentified
{
    /// <summary>
    /// Gets the immutable identifier of the object.
    /// </summary>
    /// <value>A UUID that never changes over the lifetime of the object.</value>
    Guid Id { get; }

    /// <summary>
    /// Gets the readable key of the object.
    /// </summary>
    /// <value>
    /// A key that is unique within its scope and may be renamed, for example <c>halle2.pumpe3</c> for a device or
    /// <c>druckseite</c> for a capability within its channel.
    /// </value>
    string Key { get; }

    /// <summary>
    /// Gets the former keys of the object.
    /// </summary>
    /// <value>The former keys, which stay valid for a limited time; <see langword="null"/> when there are none.</value>
    IReadOnlyList<string>? Aliases { get; }

    /// <summary>
    /// Gets the free key/value tags of the object.
    /// </summary>
    /// <value>
    /// Tags such as <c>gewerk=hlk</c> for filtering and for the automation layer; the kernel does not check them.
    /// <see langword="null"/> when there are none.
    /// </value>
    IReadOnlyDictionary<string, string>? Tags { get; }
}
