using System.Text.Json;
using System.Text.Json.Nodes;
using AwesomeAssertions;
using SmartHal.Contracts.Addressing;
using SmartHal.Contracts.Serialization;
using Xunit;

namespace SmartHal.Contracts.UnitTests.Addressing;

/// <summary>
/// Verifies the two address forms: by UUIDs with discriminator <c>by = id</c> and by keys with <c>by = key</c>.
/// </summary>
public sealed class ElementRefSerializationTests
{
    private static readonly Guid DeviceId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid ChannelId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid CapabilityId = Guid.Parse("33333333-3333-3333-3333-333333333333");

    [Fact]
    public void Serialize_Address_WritesIdDiscriminatorWithoutTheDerivedCapability()
    {
        // Arrange
        ElementRef address = new Address(DeviceId, ChannelId, CapabilityId, "value");

        // Act
        var json = JsonSerializer.Serialize(address, ContractsJson.Options);

        // Assert
        JsonNode.DeepEquals(
                JsonNode.Parse(json),
                JsonNode.Parse(
                    """{"by":"id","deviceId":"11111111-1111-1111-1111-111111111111","channelId":"22222222-2222-2222-2222-222222222222","capabilityId":"33333333-3333-3333-3333-333333333333","element":"value"}"""))
            .Should().BeTrue(json);
    }

    [Fact]
    public void Deserialize_KeyAddress_ReadsKeyForm()
    {
        // Arrange
        const string json = """{"by":"key","device":"halle2.pumpe3","channel":"hydraulik","capability":"druckseite","element":"value"}""";

        // Act
        var address = JsonSerializer.Deserialize<ElementRef>(json, ContractsJson.Options);

        // Assert
        address.Should().Be(new KeyAddress("halle2.pumpe3", "hydraulik", "druckseite", "value"));
    }

    [Fact]
    public void Deserialize_Address_ReadsIdForm()
    {
        // Arrange
        var json = JsonSerializer.Serialize<ElementRef>(new Address(DeviceId, ChannelId, CapabilityId, "on"), ContractsJson.Options);

        // Act
        var address = JsonSerializer.Deserialize<ElementRef>(json, ContractsJson.Options);

        // Assert
        address.Should().Be(new Address(DeviceId, ChannelId, CapabilityId, "on"));
    }

    [Fact]
    public void Capability_Address_ReturnsTheOwningCapability()
    {
        // Arrange
        var address = new Address(DeviceId, ChannelId, CapabilityId, "value");

        // Act
        var capability = address.Capability;

        // Assert
        capability.Should().Be(new CapabilityAddress(DeviceId, ChannelId, CapabilityId));
    }

    [Fact]
    public void Deserialize_MissingDiscriminator_ThrowsJsonException()
    {
        // Arrange
        const string json = """{"device":"a","channel":"b","capability":"c","element":"d"}""";

        // Act
        var act = () => JsonSerializer.Deserialize<ElementRef>(json, ContractsJson.Options);

        // Assert
        act.Should().Throw<JsonException>().WithMessage("*discriminator*");
    }
}
