# SmartHal.IntegrationTests

These tests drive the **real server host in process**: `ServerHost.RunAsync` runs in a background
task, the test overrides its configuration and services, waits for readiness, reads the log events
and shuts the host down again. The host fixture that does all of this is `ServerHostHarness`, and it
is a **deliverable of slice 0 for every following slice** (FR-63, FR-64) — a test never writes its
own start-up code.

## The fixture in one test

```csharp
[Collection("ProcessEnvironment")]
public sealed class MyFeatureTests
{
    [Fact]
    public async Task GivenX_WhenY_ThenZ()
    {
        await using var harness = await ServerHostHarness.StartAsync(
            options =>
            {
                options.Configuration["SmartHal:InstanceName"] = "my-test";
                options.ConfigureServices = services => services.AddSingleton<IMyPort, MyTestDouble>();
            },
            TestContext.Current.CancellationToken);

        await harness.WaitForReadyAsync();

        // ... exercise the feature through harness.Services ...

        (await harness.StopAsync()).Should().Be(ExitCodes.Success);
    }
}
```

`Lifecycle/HarnessTests.cs` is the worked example of exactly this shape (AC-32).

## What the fixture offers

| Member | What it does |
| --- | --- |
| `StartAsync(configure, cancellationToken)` | Starts the host and returns as soon as it has reached its hosted services **or** has ended (an aborted start, for example an invalid configuration). |
| `WaitForReadyAsync(timeout)` | Waits until the readiness signal reports ready — the log event **and** the `ready` health checks. Throws `TimeoutException` with an excerpt of the log after ten seconds by default. |
| `Services` | The service provider of the running host. Throws when the host never started; read `Completion` for the exit code then. |
| `Logs` | The `FakeLogCollector` of the host. `GetSnapshot()` carries the `EventId` in `record.Id.Id`, so assertions pick events by their number from `LogEvents`. |
| `Started` / `Stopped` | Whether the host started and stopped its hosted services. |
| `StopAsync()` | Asks the host to shut down through `IHostApplicationLifetime` and returns the exit code. |
| `SignalAsync(PosixSignal)` | Hands one shutdown signal to `ShutdownSignalHandler` and returns the exit code. |
| `Completion` | The task carrying the exit code of `ServerHost.RunAsync`. |
| `TempDirectory` | An empty directory of this harness; it is deleted on disposal. |
| `ReadSerilogJsonLog()` | Every line Serilog wrote to the redirected rolling file, as `JsonElement`s. Only after the host has ended. |

`HarnessOptions` carries the overrides: `Configuration` (an in-memory source appended **behind** the
command line, so it always wins), `EnvironmentName`, `Args`, `EnvironmentVariables`,
`ConfigureServices`, `UseTemporaryDataDirectory`, `CaptureSerilogJson` and `TerminateRecorder`.

## Conventions

- **Test names**: `Given…_When…_Then…` without a requirement id (C-3). The id belongs in the
  traceability matrix of the slice.
- **Collection**: the harness sets environment variables process wide and restores them on disposal,
  so every test that uses it belongs in `[Collection("ProcessEnvironment")]`.
- **Cancellation**: pass `TestContext.Current.CancellationToken` to every call that takes one
  (`xUnit1051` is an error here).
- **Disposal**: `await using var harness = …` — disposal stops the host, restores the environment
  variables and deletes the temporary directory.
- **Stack**: xUnit v3, AwesomeAssertions, FakeItEasy, `Microsoft.Extensions.Diagnostics.Testing`. No
  Serilog type ever appears in a test (C-6); read what Serilog wrote through `ReadSerilogJsonLog()`.
- **Test doubles for hosted services** are small test classes rather than fakes — their lifecycle
  behaviour is the subject under test (see `Lifecycle/ShutdownTests.cs`).

## Signals

Signals are exercised **in process** through `harness.SignalAsync(PosixSignal.SIGTERM)`, never sent
to the operating system: Windows knows no `SIGTERM`, and a real signal would hit the test runner
(C-4, Constraint 16). A second `SIGINT` would end the process in production; inside the harness it
records its exit code in `Options.TerminateRecorder.ExitCodes` instead. Real signals against a real
process are a manual step, recorded in the implementation report of the slice.

## Adding a test for a following slice

1. Put it in a folder named after the area it covers (`Configuration/`, `Health/`, `Lifecycle/`, …).
2. Start from the fixture — never call `ServerHost.RunAsync` directly, and never build your own host.
3. Override only what the test needs; the fixture already supplies a valid configuration and a
   temporary data directory.
4. Assert on `EventId`s from `LogEvents` rather than on message text, so a reworded message does not
   break the test.
