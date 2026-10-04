namespace SmartHal.Contracts.Primitives;

/// <summary>
/// Defines the identity every addressable instance object carries: an immutable id and a readable key.
/// </summary>
public interface IIdentified
{
    /// <summary>
    /// Gets the immutable identifier of the object.
    /// </summary>
    Guid Id { get; }

    /// <summary>
    /// Gets the readable key of the object, unique within its scope, for example <c>halle2.pumpe3</c>.
    /// </summary>
    string Key { get; }

    /// <summary>
    /// Gets the former keys that stay valid as aliases after a rename.
    /// </summary>
    IReadOnlyList<string>? Aliases { get; }

    /// <summary>
    /// Gets the free key/value tags for filtering and automation, for example <c>gewerk=hlk</c>.
    /// </summary>
    IReadOnlyDictionary<string, string>? Tags { get; }
}
