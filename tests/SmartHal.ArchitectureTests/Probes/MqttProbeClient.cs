namespace MQTTnet.Probes;

/// <summary>
/// Stands in for an MQTT client type. The namespace is faked inside the test assembly on purpose:
/// the rule has to be provable without the repository ever referencing an MQTT library (C-5).
/// </summary>
/// <param name="topicPrefix">Prefix prepended to every published topic.</param>
internal sealed class ProbeClient(string topicPrefix)
{
    /// <summary>
    /// Returns the prefixed topic; the probe only needs a member to call.
    /// </summary>
    /// <param name="topic">Any topic.</param>
    /// <returns>The topic behind the prefix.</returns>
    public string Publish(string topic) => topicPrefix + topic;
}
