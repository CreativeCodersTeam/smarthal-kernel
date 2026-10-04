namespace SmartHal.Contracts.Integration;

/// <summary>
/// Configures the device discovery of an adapter.
/// </summary>
/// <param name="Enabled"><see langword="true"/> to let the adapter discover devices; otherwise, <see langword="false"/>.</param>
public sealed record DiscoverySettings(bool Enabled);
