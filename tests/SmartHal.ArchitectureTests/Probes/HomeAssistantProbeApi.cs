namespace HomeAssistant.Probes;

/// <summary>
/// Stands in for a Home Assistant client type. The namespace is faked inside the test assembly on
/// purpose: the rule has to be provable without the repository ever referencing a Home Assistant
/// library (C-5).
/// </summary>
/// <param name="aliasPrefix">Prefix prepended to every called alias.</param>
internal sealed class ProbeApi(string aliasPrefix)
{
    /// <summary>
    /// Returns the prefixed alias; the probe only needs a member to call.
    /// </summary>
    /// <param name="alias">Any alias.</param>
    /// <returns>The alias behind the prefix.</returns>
    public string Call(string alias) => aliasPrefix + alias;
}
