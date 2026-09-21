using Serilog.Probes;

namespace SmartHal.Server.Probes;

/// <summary>
/// Probe type that deliberately breaks <c>NamespaceRules.SerilogOnlyInCompositionRoot</c>: it lives
/// in <c>SmartHal.Server</c> but outside the composition root and touches a Serilog namespace
/// (AC-17). It lives in the test assembly, so the production assemblies stay clean.
/// </summary>
internal static class SerilogProbe
{
    private static readonly ProbeSink Sink = new("probe: ");

    /// <summary>
    /// Forwards a message to the faked Serilog sink.
    /// </summary>
    /// <param name="message">Any message.</param>
    /// <returns>The value of <paramref name="message"/>.</returns>
    public static string Emit(string message) => Sink.Emit(message);
}
