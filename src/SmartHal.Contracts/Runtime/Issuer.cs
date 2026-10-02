namespace SmartHal.Contracts.Runtime;

/// <summary>
/// Describes who issued a command.
/// </summary>
/// <param name="Kind">The kind of issuer.</param>
/// <param name="Id">The identifier of the issuer within its kind, for example a user id or an automation id.</param>
public sealed record Issuer(IssuerKind Kind, string Id);
