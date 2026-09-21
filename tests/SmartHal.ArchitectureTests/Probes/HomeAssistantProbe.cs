using HomeAssistant.Probes;

namespace SmartHal.Contracts.Probes;

/// <summary>
/// Probe type that deliberately breaks <c>NamespaceRules.NoHomeAssistantInKernelAndPublished</c>:
/// it looks like a published contract type and touches a Home Assistant namespace (AC-21). It lives
/// in the test assembly, so the production assemblies stay clean.
/// </summary>
internal static class HomeAssistantProbe
{
    private static readonly ProbeApi Api = new("probe.");

    /// <summary>
    /// Forwards an alias to the faked Home Assistant client.
    /// </summary>
    /// <param name="alias">Any alias.</param>
    /// <returns>The value of <paramref name="alias"/>.</returns>
    public static string Call(string alias) => Api.Call(alias);
}
