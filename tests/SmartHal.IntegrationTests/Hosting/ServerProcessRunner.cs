using System.Diagnostics;

namespace SmartHal.IntegrationTests.Hosting;

/// <summary>
/// Starts the built server assembly as a real child process and collects what it writes to the
/// console (C-6, AC-16).
/// </summary>
/// <remarks>
/// Console output and the rolling file can only be observed from outside the process, because the
/// in-process harness replaces neither the standard output stream nor the working directory. The
/// runner reads both output streams line by line and never touches a Serilog type, so <c>tests/</c>
/// stays free of the logging framework (C-6).
/// </remarks>
public static class ServerProcessRunner
{
    /// <summary>
    /// The hard limit a run may take before the runner kills the process.
    /// </summary>
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Runs <c>dotnet SmartHal.Server.dll</c> and returns everything the process produced.
    /// </summary>
    /// <param name="options">The working directory, environment variables, arguments and the
    /// optional stop condition of the run.</param>
    /// <returns>
    /// The exit code, the collected output lines and whether the runner had to kill the process.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> is <see langword="null"/>.</exception>
    /// <exception cref="FileNotFoundException">
    /// The server assembly is missing from the output directory of the test project.
    /// </exception>
    public static async Task<ProcessRunResult> RunAsync(ProcessRunOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var serverAssembly = Path.Combine(AppContext.BaseDirectory, "SmartHal.Server.dll");

        if (!File.Exists(serverAssembly))
        {
            throw new FileNotFoundException(
                "The server assembly is not part of the test output directory.",
                serverAssembly);
        }

        var standardOutput = new List<string>();
        var standardError = new List<string>();
        var gate = new Lock();
        var killed = false;

        using var process = new Process { StartInfo = StartInfoFor(options, serverAssembly) };

        process.OutputDataReceived += (_, arguments) =>
        {
            if (arguments.Data is null)
            {
                return;
            }

            lock (gate)
            {
                standardOutput.Add(arguments.Data);
            }

            if (options.StopWhenStdOutMatches?.Invoke(arguments.Data) == true)
            {
                lock (gate)
                {
                    killed = true;
                }

                Kill(process);
            }
        };

        process.ErrorDataReceived += (_, arguments) =>
        {
            if (arguments.Data is null)
            {
                return;
            }

            lock (gate)
            {
                standardError.Add(arguments.Data);
            }
        };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        using var timeout = new CancellationTokenSource(Timeout);

        try
        {
            await process.WaitForExitAsync(timeout.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            lock (gate)
            {
                killed = true;
            }

            Kill(process);
            await process.WaitForExitAsync(CancellationToken.None).ConfigureAwait(false);
        }

        lock (gate)
        {
            return new ProcessRunResult(process.ExitCode, [.. standardOutput], [.. standardError], killed);
        }
    }

    private static ProcessStartInfo StartInfoFor(ProcessRunOptions options, string serverAssembly)
    {
        var startInfo = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = options.WorkingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };

        startInfo.ArgumentList.Add(serverAssembly);

        foreach (var argument in options.Args)
        {
            startInfo.ArgumentList.Add(argument);
        }

        foreach (var variable in options.EnvironmentVariables)
        {
            if (variable.Value is null)
            {
                startInfo.Environment.Remove(variable.Key);
            }
            else
            {
                startInfo.Environment[variable.Key] = variable.Value;
            }
        }

        return startInfo;
    }

    private static void Kill(Process process)
    {
        // The process may have ended between the matching line and this call; that race is the
        // normal case for a short-lived run and must not fail the test that observed the line.
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch (InvalidOperationException)
        {
            // Ignored on purpose, see above.
        }
        catch (SystemException)
        {
            // Win32Exception derives from SystemException and is raised when the process is already
            // gone; ignored for the same reason.
        }
    }
}

/// <summary>
/// The settings a single <see cref="ServerProcessRunner.RunAsync"/> call is made with.
/// </summary>
public sealed class ProcessRunOptions
{
    /// <summary>
    /// Gets or sets the directory the child process runs in.
    /// </summary>
    /// <value>
    /// The working directory. It is also the content root of the host and the anchor of the
    /// relative log path <c>./logs</c>, so a test points it at its own temporary directory.
    /// </value>
    public required string WorkingDirectory { get; set; }

    /// <summary>
    /// Gets the environment variables the child process is started with.
    /// </summary>
    /// <value>
    /// A map of variable name to value on top of the inherited environment; a
    /// <see langword="null"/> value removes an inherited variable.
    /// </value>
    public Dictionary<string, string?> EnvironmentVariables { get; } = new(StringComparer.Ordinal);

    /// <summary>
    /// Gets the command line arguments the server assembly is started with.
    /// </summary>
    /// <value>The arguments behind the path of the server assembly.</value>
    public List<string> Args { get; } = [];

    /// <summary>
    /// Gets or sets the condition that ends the run early.
    /// </summary>
    /// <value>
    /// A predicate evaluated for every line on standard output; the runner kills the process as soon
    /// as it returns <see langword="true"/>. <see langword="null"/> lets the process run into the
    /// timeout.
    /// </value>
    public Func<string, bool>? StopWhenStdOutMatches { get; set; }
}

/// <summary>
/// What a <see cref="ServerProcessRunner.RunAsync"/> call observed.
/// </summary>
/// <param name="ExitCode">The exit code of the child process; meaningless when it was killed.</param>
/// <param name="StdOut">The lines the process wrote to standard output, in order.</param>
/// <param name="StdErr">The lines the process wrote to standard error, in order.</param>
/// <param name="Killed">
/// <see langword="true"/> when the stop condition matched or the timeout elapsed.
/// </param>
public sealed record ProcessRunResult(
    int ExitCode,
    IReadOnlyList<string> StdOut,
    IReadOnlyList<string> StdErr,
    bool Killed);
