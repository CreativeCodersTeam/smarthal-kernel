using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using SmartHal.IntegrationTests.Hosting;
using SmartHal.Server.Composition;
using SmartHal.Server.Configuration;
using SmartHal.Server.Diagnostics;
using SmartHal.Server.Health;
using Xunit;

namespace SmartHal.IntegrationTests.Health;

/// <summary>
/// Drives the server host in process and verifies the readiness signal: the registered checks, their
/// tags, the event of the ready state, and that no transport carries any of it (AC-9, FR-39, FR-41,
/// FR-43).
/// </summary>
/// <remarks>
/// The harness sets process-wide environment variables, so the tests run in the serialised
/// <c>ProcessEnvironment</c> collection.
/// </remarks>
[Collection("ProcessEnvironment")]
public sealed class ReadinessTests
{
    [Fact]
    public async Task GivenValidConfiguration_WhenHostIsStarted_ThenAllReadyChecksPassAndReadyEventIsLogged()
    {
        // Arrange
        await using var harness = await ServerHostHarness.StartAsync(
            cancellationToken: TestContext.Current.CancellationToken);

        // Act
        await harness.WaitForReadyAsync();

        // Assert
        var healthCheckService = harness.Services.GetRequiredService<HealthCheckService>();
        var report = await healthCheckService.CheckHealthAsync(
            registration => registration.Tags.Contains(HealthTags.Ready),
            TestContext.Current.CancellationToken);

        report.Status.Should().Be(HealthStatus.Healthy, "AC-9 expects every ready check to pass");
        report.Entries.Should().ContainKey(
            "configuration",
            "FR-43 requires a ready check that reports the validated configuration");

        EventIdsOf(harness).Should().Contain(
            LogEvents.Health.HealthReady,
            "FR-42 writes the ready state with its own EventId");
        EventIdsOf(harness).Should().Contain(
            LogEvents.Health.HealthStarting,
            "the readiness signal opens with the starting state");

        (await harness.StopAsync()).Should().Be(ExitCodes.Success);
    }

    [Fact]
    public async Task GivenStartedHost_WhenHealthChecksAreListed_ThenEveryCheckCarriesLiveAndReadyTags()
    {
        // Arrange
        await using var harness = await ServerHostHarness.StartAsync(
            cancellationToken: TestContext.Current.CancellationToken);

        await harness.WaitForReadyAsync();

        // Act
        var registrations = harness.Services
            .GetRequiredService<IOptions<HealthCheckServiceOptions>>()
            .Value
            .Registrations;

        // Assert
        registrations.Should().NotBeEmpty("FR-39 registers at least one health check");

        foreach (var registration in registrations)
        {
            registration.Tags.Should().Contain(HealthTags.Live, $"'{registration.Name}' must answer liveness");
            registration.Tags.Should().Contain(HealthTags.Ready, $"'{registration.Name}' must answer readiness");
        }
    }

    [Fact]
    public async Task GivenStartedHost_WhenServicesInspected_ThenNoHealthTransportIsRegistered()
    {
        // Arrange
        IServiceCollection? registeredServices = null;

        await using var harness = await ServerHostHarness.StartAsync(
            options => options.ConfigureServices = services => registeredServices = services,
            TestContext.Current.CancellationToken);

        await harness.WaitForReadyAsync();

        // Act
        registeredServices.Should().NotBeNull();

        var registeredTypeNames = registeredServices
            .SelectMany(descriptor => new[] { descriptor.ServiceType, descriptor.ImplementationType })
            .Where(type => type is not null)
            .Select(type => type!.FullName ?? type.Name)
            .ToArray();

        // Assert
        // FR-41: the readiness state is readable in process and in the log, and nowhere else - no web
        // host or server, no socket listener, and no file the state would be written to.
        // "Listener" alone would also match the metrics configuration types of the host, which carry
        // no transport; the names below are the ones a transport would actually register.
        string[] transportTypeNames =
        [
            "Microsoft.AspNetCore",
            "Kestrel",
            "IServer",
            "HttpListener",
            "System.Net.Sockets",
            "TcpListener",
            "UnixDomainSocket"
        ];

        foreach (var forbidden in transportTypeNames)
        {
            registeredTypeNames.Should().NotContain(
                name => name.Contains(forbidden, StringComparison.Ordinal),
                $"FR-41 forbids a transport for the readiness state, but a service mentions '{forbidden}'");
        }

        var dataDirectory = harness.Services.GetRequiredService<IOptions<SmartHalOptions>>().Value.DataDirectory;
        Directory.GetFileSystemEntries(dataDirectory).Should().BeEmpty(
            "FR-41 forbids a readiness file below the data directory");
    }

    private static int[] EventIdsOf(ServerHostHarness harness)
    {
        return [.. harness.Logs.GetSnapshot().Select(record => record.Id.Id)];
    }
}
