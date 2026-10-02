namespace SmartHal.Contracts.Runtime;

/// <summary>
/// Specifies who issued a command.
/// </summary>
public enum IssuerKind
{
    /// <summary>A user.</summary>
    User = 0,

    /// <summary>An automation or a scene.</summary>
    Automation = 1,

    /// <summary>The platform itself.</summary>
    System = 2
}
