namespace SmartHal.Contracts.Integration;

/// <summary>
/// Configures the device discovery of an adapter.
/// </summary>
/// <remarks>
/// A discovering adapter places a discovery result in the inbox for every device it finds; the user approves,
/// rejects or ignores it there.
/// </remarks>
/// <param name="Enabled"><see langword="true"/> to let the adapter discover devices; otherwise, <see langword="false"/>.</param>
public sealed record DiscoverySettings(bool Enabled);
