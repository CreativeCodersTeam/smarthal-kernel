namespace Serilog.Probes;

/// <summary>
/// Stands in for a Serilog sink type. The namespace is faked inside the test assembly on purpose:
/// tests/ must not have to reference Serilog to prove the rule (C-5, C-6).
/// </summary>
/// <param name="messagePrefix">Prefix prepended to every emitted message.</param>
internal sealed class ProbeSink(string messagePrefix)
{
    /// <summary>
    /// Returns the prefixed message; the probe only needs a member to call.
    /// </summary>
    /// <param name="message">Any message.</param>
    /// <returns>The message behind the prefix.</returns>
    public string Emit(string message) => messagePrefix + message;
}
