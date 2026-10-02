namespace SmartHal.Core.Abstractions.Validation;

/// <summary>
/// Describes one violation of a structural rule of the contracts.
/// </summary>
/// <param name="Path">
/// The location of the violation relative to the validated object, in camelCase JSON notation, for example
/// <c>commands.setLevel.affects[0]</c> or <c>channels[1].capabilities[0].typeRef</c>; empty for the object itself.
/// </param>
/// <param name="Code">The machine-readable code of the violated rule; see <see cref="ValidationCodes"/>.</param>
/// <param name="Message">A human-readable description of the violation.</param>
public sealed record ValidationError(string Path, string Code, string Message);
