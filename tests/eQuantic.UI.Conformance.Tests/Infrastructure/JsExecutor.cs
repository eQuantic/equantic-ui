using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Runtime.InteropServices;

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
    private static string? _bunPath;
    private static bool? _bunWorks;
    private static bool? _nodeWorks;

    public static bool IsAvailable => BunWorks() || NodeWorks();

    /// <summary>The engine that will actually run scripts: "bun", "node", or "none".</summary>
    public static string EngineName => BunWorks() ? "bun" : NodeWorks() ? "node" : "none";

    public static string Run(string jsProgram, int timeoutMs = 20000)
    {
        var scriptPath = Path.Combine(Path.GetTempPath(), $"eq-conformance-{Guid.NewGuid():N}.mjs");
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

    private static string? BunPath()
    {
        if (_bunPath != null) return _bunPath;
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
                // Extraction failed (corrupt zip, perms): leave _bunPath null so we fall back to Node.
            }
        }

        _bunPath = File.Exists(exePath) ? exePath : null;
        return _bunPath;
    }

    private static bool BunWorks()
    {
        if (_bunWorks.HasValue) return _bunWorks.Value;
        _bunWorks = CanExecute(() =>
        {
            var bun = BunPath();
            if (bun == null) return null;
            var probe = Path.Combine(Path.GetTempPath(), $"eq-probe-{Guid.NewGuid():N}.mjs");
            File.WriteAllText(probe, "console.log('ok')");
            try { return RunProcess(bun, new[] { "run", probe }, 10000); }
            finally { try { File.Delete(probe); } catch { } }
        });
        return _bunWorks.Value;
    }

    private static bool NodeWorks()
    {
        if (_nodeWorks.HasValue) return _nodeWorks.Value;
        _nodeWorks = CanExecute(() =>
        {
            try { return RunProcess("node", new[] { "-e", "console.log('ok')" }, 10000); }
            catch { return null; }
        });
        return _nodeWorks.Value;
    }

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

    private static (int exitCode, string stdout, string stderr) RunProcess(string fileName, string[] args, int timeoutMs)
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
