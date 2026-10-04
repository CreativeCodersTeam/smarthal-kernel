using System.ComponentModel.DataAnnotations;

namespace SmartHal.Server.Configuration;

/// <summary>
/// Represents the <c>SmartHal</c> configuration section of the server.
/// </summary>
/// <remarks>
/// The properties use plain setters with empty defaults instead of <c>required</c>, so a missing value
/// surfaces as a validation message naming the field rather than as a binding error.
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
    /// <value>A mandatory name of at most 64 characters.</value>
    [Required]
    public string InstanceName { get; set; } = "";

    /// <summary>
    /// Gets or sets the directory the server keeps its data in.
    /// </summary>
    /// <value>A mandatory, writable path; <c>~</c> and placeholders are expanded, see <see cref="PathExpansion"/>.</value>
    [Required]
    public string DataDirectory { get; set; } = "";

    /// <summary>
    /// Gets or sets how long an orderly shutdown may take.
    /// </summary>
    /// <value>A span greater than zero and at most five minutes. The default is 30 seconds.</value>
    public TimeSpan ShutdownTimeout { get; set; } = TimeSpan.FromSeconds(30);
}
