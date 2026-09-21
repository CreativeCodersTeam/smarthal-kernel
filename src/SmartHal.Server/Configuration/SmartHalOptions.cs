using System.ComponentModel.DataAnnotations;

namespace SmartHal.Server.Configuration;

/// <summary>
/// The options of the <c>SmartHal</c> configuration section (IF-4, FR-21).
/// </summary>
/// <remarks>
/// <para>
/// The section is bound once at start and validated before any hosted service runs; the rules that
/// go beyond the attributes below live in <see cref="SmartHalOptionsValidator"/> (FR-22).
/// </para>
/// <para>
/// The properties carry ordinary setters and an empty default instead of <c>required</c> or
/// <c>init</c>: the configuration binder assigns them by reflection after it has created the
/// instance, and a missing mandatory value has to surface as a validation message that names the
/// field rather than as a binding error (FR-24).
/// </para>
/// </remarks>
public sealed class SmartHalOptions
{
    /// <summary>
    /// The name of the configuration section these options are bound to.
    /// </summary>
    public const string SectionName = "SmartHal";

    /// <summary>
    /// Gets or sets the name of this server instance.
    /// </summary>
    /// <value>
    /// A mandatory name of at most 64 characters that is neither empty nor whitespace only. It has
    /// no default; an unconfigured section leaves it empty and makes the configuration invalid.
    /// </value>
    [Required]
    public string InstanceName { get; set; } = "";

    /// <summary>
    /// Gets or sets the directory the server keeps its data in.
    /// </summary>
    /// <value>
    /// A mandatory path. It is created when it does not exist and has to be writable; it has no
    /// default.
    /// </value>
    [Required]
    public string DataDirectory { get; set; } = "";

    /// <summary>
    /// Gets or sets how long an orderly shutdown may take.
    /// </summary>
    /// <value>
    /// A span greater than zero and at most <c>00:05:00</c>. The default is <c>00:00:30</c>.
    /// </value>
    public TimeSpan ShutdownTimeout { get; set; } = TimeSpan.FromSeconds(30);
}
