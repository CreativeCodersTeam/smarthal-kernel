using SmartHal.Contracts.DataTypes;

namespace SmartHal.Contracts.Schema;

/// <summary>
/// Defines a command of a capability type.
/// </summary>
/// <param name="Completion">When the command counts as completed.</param>
/// <param name="Timeout">How long the command may take before it ends as timed out.</param>
/// <param name="Parameters">The parameters, keyed by name; <see langword="null"/> when the command takes none.</param>
/// <param name="Result">The data type of the result; mandatory for <see cref="Schema.Completion.Result"/>, otherwise <see
/// langword="null"/>.</param>
/// <param name="Affects">The names of the properties of this capability the command changes; <see langword="null"/> when it changes
/// none.</param>
/// <param name="Feature">The feature flag the command depends on; <see langword="null"/> when it is always present.</param>
/// <param name="RequiredParameters">The names of the parameters a call has to supply; <see langword="null"/> when every parameter is
/// optional.</param>
public sealed record CommandDef(
    Completion Completion,
    TimeSpan Timeout,
    IReadOnlyDictionary<string, DataType>? Parameters = null,
    DataType? Result = null,
    IReadOnlyList<string>? Affects = null,
    string? Feature = null,
    IReadOnlyList<string>? RequiredParameters = null);
