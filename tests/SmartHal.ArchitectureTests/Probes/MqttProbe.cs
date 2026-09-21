using MQTTnet.Probes;

namespace SmartHal.Core.Probes;

/// <summary>
/// Probe type that deliberately breaks <c>NamespaceRules.NoMqttInKernelAndPublished</c>: it looks
/// like a kernel type and touches an MQTT namespace (AC-21). It lives in the test assembly, so the
/// production assemblies stay clean.
/// </summary>
internal static class MqttProbe
{
    private static readonly ProbeClient Client = new("probe/");

    /// <summary>
    /// Forwards a topic to the faked MQTT client.
    /// </summary>
    /// <param name="topic">Any topic.</param>
    /// <returns>The value of <paramref name="topic"/>.</returns>
    public static string Publish(string topic) => Client.Publish(topic);
}
