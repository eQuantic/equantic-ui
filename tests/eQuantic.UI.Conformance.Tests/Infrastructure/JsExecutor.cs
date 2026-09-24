using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Threading;

namespace eQuantic.UI.Conformance.Tests.Infrastructure;

/// <summary>
/// Executes JavaScript for the conformance harness. The PRIMARY engine is the project's embedded
/// Bun binary — resolved exactly like the SDK does: per-OS <b>and per-architecture</b> (the native
/// arm64 build on Apple Silicon / arm64 Linux·Windows, the x64-baseline build elsewhere), extracted
/// on demand from the tracked <c>.zip</c> if the binary isn't already unpacked. If Bun is present but
/// cannot execute JS on the current machine (e.g. an AVX-less VM where this Bun build crashes at
/// startup), the harness falls back to a local Node, purely so development/CI isn't blocked. This is
/// an internal test-only fallback and does NOT relax the shipped SDK's "no Node/npm required"
/// guarantee — that path still uses Bun. Only stdout is returned; stderr is surfaced only on failure.
/// </summary>
public static class JsExecutor
{
    /// <summary>
    /// Answered ONCE, by one thread, in this order: where Bun is (extracting it if the tree only
    /// has the zip), then whether it runs, then — only if it does not — whether Node does.
    /// <para>
    /// These were three <c>bool?</c> fields written outside any lock, and xUnit runs test classes
    /// in parallel. Two classes arriving together both probed and both wrote, so the answer a test
    /// read was whichever probe finished last. On a cold Windows runner that put the on-demand
    /// unzip of <c>bun.exe</c> in one thread against a ten-second probe of that same file in
    /// another, and ONE test in an otherwise green suite was told no JS engine works while every
    /// other test in the same process ran Bun perfectly. A harness that reports an engine the suite
    /// is demonstrably using is worse than no report at all.
    /// </para>
    /// <para>
    /// <see cref="LazyThreadSafetyMode.ExecutionAndPublication"/> is the mechanism .NET already has
    /// for this: the factory runs exactly once, nobody sees a half-extracted binary, and the probe
    /// times the ENGINE rather than whatever else was writing to it.
    /// </para>
    /// </summary>
    private static readonly Lazy<string?> Bun =
        new(ResolveBun, LazyThreadSafetyMode.ExecutionAndPublication);

    private static readonly Lazy<bool> BunRuns =
        new(ProbeBun, LazyThreadSafetyMode.ExecutionAndPublication);

    private static readonly Lazy<bool> NodeRuns =
        new(ProbeNode, LazyThreadSafetyMode.ExecutionAndPublication);

    public static bool IsAvailable
    {
        get
        {
            var available = BunWorks() || NodeWorks();
            if (!available && Environment.GetEnvironmentVariable("EQ_REQUIRE_JS") == "1")
                throw new InvalidOperationException(
                    "EQ_REQUIRE_JS=1 but no JS engine works (embedded Bun crashed and Node is absent). " +
                    "Conformance must FAIL loudly here, not skip silently — fix the runner environment.");
            return available;
        }
    }

    /// <summary>The engine that will actually run scripts: "bun", "node", or "none".</summary>
    public static string EngineName => BunWorks() ? "bun" : NodeWorks() ? "node" : "none";

    /// <summary>The embedded Bun the SDK ships, when it runs here: what a test hands to code that
    /// bundles as the build does, rather than a script to run.</summary>
    public static string? BunExecutable => BunWorks() ? BunPath() : null;

    public static string Run(string jsProgram, int timeoutMs = 20000)
    {
        // The program is the TYPESCRIPT the SDK emits — a declaration whose type differs from its
        // initializer carries an annotation (`let m: Money = Money.fromInt(5)`). Bun runs .ts as is,
        // which is the SDK's own pipeline; the node fallback gets plain .mjs and only sees the
        // annotation-free shapes.
        var scriptPath = Path.Combine(Path.GetTempPath(), $"eq-conformance-{Guid.NewGuid():N}{(BunWorks() ? ".ts" : ".mjs")}");
        File.WriteAllText(scriptPath, jsProgram);
        try
        {
            if (BunWorks())
            {
                var (code, stdout, stderr) = RunProcess(BunPath()!, new[] { "run", scriptPath }, timeoutMs);
                if (code != 0) throw Failure("bun", code, jsProgram, stdout, stderr);
                return stdout.Trim();
            }

            if (NodeWorks())
            {
                var (code, stdout, stderr) = RunProcess("node", new[] { scriptPath }, timeoutMs);
                if (code != 0) throw Failure("node", code, jsProgram, stdout, stderr);
                return stdout.Trim();
            }

            throw new InvalidOperationException("No JS engine available (neither embedded Bun nor Node could run).");
        }
        finally
        {
            try { File.Delete(scriptPath); } catch { /* best effort */ }
        }
    }

    private static string? BunPath() => Bun.Value;

    private static string? ResolveBun()
    {
        var root = RepoRoot.Find();
        if (root == null) return null;

        // Mirror the SDK (Sdk.targets): pick the per-OS + per-architecture embedded-bun package —
        // the native arm64 build on Apple Silicon / arm64 Linux·Windows, the x64-baseline elsewhere.
        var arm64 = RuntimeInformation.OSArchitecture == Architecture.Arm64;
        var packageId =
            RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? (arm64 ? "eQuantic.UI.Runtime.OsxArm64" : "eQuantic.UI.Runtime.Osx64")
            : RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ? (arm64 ? "eQuantic.UI.Runtime.LinuxArm64" : "eQuantic.UI.Runtime.Linux64")
            : RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? (arm64 ? "eQuantic.UI.Runtime.WinArm64" : "eQuantic.UI.Runtime.Win64")
            : null;
        if (packageId == null) return null;

        var exeName =
            RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "bun.exe"
            : RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ? "bun-linux"
            : "bun-darwin";

        var bunDir = Path.Combine(root, "src", packageId, "tools", "bun");
        var exePath = Path.Combine(bunDir, exeName);
        var zipPath = exePath + ".zip"; // bun-darwin.zip / bun-linux.zip / bun.exe.zip

        // Only the .zip is tracked in git; the SDK extracts the binary on first build. Mirror that here
        // so the harness runs on the same Bun the framework ships instead of silently using Node.
        if (!File.Exists(exePath) && File.Exists(zipPath))
        {
            try
            {
                ZipFile.ExtractToDirectory(zipPath, bunDir, overwriteFiles: true);
                if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && File.Exists(exePath))
                {
                    File.SetUnixFileMode(exePath,
                        UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute |
                        UnixFileMode.GroupRead | UnixFileMode.GroupExecute |
                        UnixFileMode.OtherRead | UnixFileMode.OtherExecute);
                }
            }
            catch
            {
                // Extraction failed (corrupt zip, perms): answer null so we fall back to Node.
            }
        }

        return File.Exists(exePath) ? exePath : null;
    }

    private static bool BunWorks() => BunRuns.Value;

    private static bool ProbeBun() => CanExecute(() =>
    {
        var bun = BunPath();
        if (bun == null) return null;
        var probe = Path.Combine(Path.GetTempPath(), $"eq-probe-{Guid.NewGuid():N}.mjs");
        File.WriteAllText(probe, "console.log('ok')");
        try { return RunProcess(bun, new[] { "run", probe }, 10000); }
        finally { try { File.Delete(probe); } catch { } }
    });

    private static bool NodeWorks() => NodeRuns.Value;

    private static bool ProbeNode() => CanExecute(() =>
    {
        try { return RunProcess("node", new[] { "-e", "console.log('ok')" }, 10000); }
        catch { return null; }
    });

    private static bool CanExecute(Func<(int, string, string)?> probe)
    {
        try
        {
            var result = probe();
            return result is { } r && r.Item1 == 0 && r.Item2.Trim() == "ok";
        }
        catch
        {
            return false;
        }
    }

    /// <summary>Runs a process to its end and answers what it wrote. One that outlives
    /// <paramref name="timeoutMs"/> is killed with every process it started, rather than left
    /// running past the test that started it.</summary>
    internal static (int exitCode, string stdout, string stderr) RunProcess(string fileName, string[] args, int timeoutMs)
    {
        var psi = new ProcessStartInfo(fileName)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        foreach (var a in args) psi.ArgumentList.Add(a);

        using var process = Process.Start(psi)
            ?? throw new InvalidOperationException($"Failed to start process: {fileName}");

        var stdoutTask = process.StandardOutput.ReadToEndAsync();
        var stderrTask = process.StandardError.ReadToEndAsync();

        if (!process.WaitForExit(timeoutMs))
        {
            try { process.Kill(entireProcessTree: true); } catch { /* best effort */ }
            throw new TimeoutException($"{fileName} timed out after {timeoutMs}ms.");
        }

        return (process.ExitCode, stdoutTask.GetAwaiter().GetResult(), stderrTask.GetAwaiter().GetResult());
    }

    private static InvalidOperationException Failure(string engine, int code, string program, string stdout, string stderr) =>
        new($"{engine} exited with code {code}.\nProgram:\n{program}\nstderr:\n{stderr}\nstdout:\n{stdout}");
}
