using System.Runtime.InteropServices;
using eQuantic.UI.Sdk.Tasks;

namespace eQuantic.UI.Tests;

/// <summary>
/// The source tree's bun for this machine, chosen as the SDK chooses it (Sdk.targets) and as
/// <c>src/Shared/EmbeddedBun.targets</c> resolves it for the Server's bundle: the per-OS, per-architecture
/// runtime package's zip, the native arm64 build on an arm64 machine and the x64-baseline elsewhere, unpacked
/// into the user's cache under the zip's SHA-256 (<see cref="ProgramArchive"/>), so a test runs the bun the zip
/// holds and the one a build bundled with (#932).
/// </summary>
internal static class EmbeddedBun
{
    /// <summary>The zip of this machine's bun under <paramref name="root"/>, or null where the SDK ships none.</summary>
    internal static string? Archive(string root)
    {
        var arm64 = RuntimeInformation.OSArchitecture == Architecture.Arm64;
        var (package, zip) =
            RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? (arm64 ? "eQuantic.UI.Runtime.OsxArm64" : "eQuantic.UI.Runtime.Osx64", "bun-darwin.zip")
            : RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ? (arm64 ? "eQuantic.UI.Runtime.LinuxArm64" : "eQuantic.UI.Runtime.Linux64", "bun-linux.zip")
            : RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? (arm64 ? "eQuantic.UI.Runtime.WinArm64" : "eQuantic.UI.Runtime.Win64", "bun.exe.zip")
            : (null, null);
        return package is null ? null : Path.Combine(root, "src", package, "tools", "bun", zip!);
    }

    /// <summary>The bun the zip holds, unpacked where the SDK unpacks it, or null where there is no zip.</summary>
    internal static string? Program(string root) =>
        Archive(root) is { } zip && File.Exists(zip) ? ProgramArchive.Unpack(zip, ProgramArchive.DefaultCache()).Program : null;
}
