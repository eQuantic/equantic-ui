using System.ComponentModel;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;

namespace eQuantic.UI.Sdk.Tasks;

/// <summary>
/// A program that ships zipped, unpacked where the build may write and named by what the zip holds.
/// The embedded bun is the one such program: its zip rides in the per-OS runtime package, and every
/// build that bundles needs it ready to run.
/// </summary>
/// <remarks>
/// <para>
/// The bun was unpacked beside its zip, which put it in two places it did not belong. In the NuGet
/// package cache (#364): a cache restored read-only, baked into an image or shared on a volume
/// failed the build at the unzip, two builds on a clean cache unpacked into one folder, and NuGet
/// never knew of a file its package does not hold. And in a source tree (#932), where a binary
/// unpacked once was kept after the tracked zip moved to another version: a worktree ran bun 1.3.14
/// beside a zip of 1.4.3, bundled every module with it, and failed two conformance cases no other
/// worktree failed, with nothing saying which bun ran.
/// </para>
/// <para>
/// Both are the same question, answered once here: the program is unpacked into a folder named by
/// the SHA-256 of its zip, under a cache of the user's that nothing else writes. A zip that changes
/// is a folder that is not there yet, so the program that runs is always the zip's, and the same zip
/// in a package cache, in a source tree or in every worktree of one is unpacked once. The folder
/// appears in one rename, after the program is complete and executable, so a build never sees half
/// of one, and two builds unpacking the same zip at once leave one folder. The SDK's targets, the
/// source tree's Runtime and Server projects and the conformance harness all ask this class.
/// </para>
/// </remarks>
public static class ProgramArchive
{
    /// <summary>rwxr-xr-x: what <c>chmod +x</c> left on a file unpacked with rw-r--r--.</summary>
    private const int Executable = 0x1ED;

    /// <summary>
    /// The cache this machine keeps for a user's programs, as the system names it: the local
    /// application data on Windows, <c>~/Library/Caches</c> on macOS, and <c>$XDG_CACHE_HOME</c>
    /// or <c>~/.cache</c> elsewhere.
    /// </summary>
    /// <exception cref="InvalidOperationException">There is no home folder to keep it in.</exception>
    public static string DefaultCache()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "eQuantic", "Cache");

        var home = Environment.GetEnvironmentVariable("HOME");
        if (string.IsNullOrEmpty(home))
            throw new InvalidOperationException("HOME is not set, so there is no user cache to unpack a program into.");
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            return Path.Combine(home, "Library", "Caches", "eQuantic");

        var xdg = Environment.GetEnvironmentVariable("XDG_CACHE_HOME");
        return Path.Combine(string.IsNullOrEmpty(xdg) ? Path.Combine(home, ".cache") : xdg!, "eQuantic");
    }

    /// <summary>
    /// The program <paramref name="archive"/> holds, ready to run under <paramref name="cache"/>, and
    /// whether this call is the one that unpacked it. The program is the zip's entry named like the
    /// zip without its <c>.zip</c>, as <c>bun-darwin.zip</c> holds <c>bun-darwin</c>, and it lands at
    /// <c>&lt;cache&gt;/&lt;program&gt;/&lt;sha-256 of the zip&gt;/&lt;program&gt;</c>.
    /// </summary>
    /// <exception cref="InvalidDataException">The zip holds no entry of that name.</exception>
    public static (string Program, bool Unpacked) Unpack(string archive, string cache)
    {
        var name = Path.GetFileNameWithoutExtension(archive);
        var folder = Path.Combine(cache, name, Digest(archive));
        var program = Path.Combine(folder, name);
        if (File.Exists(program)) return (program, false);

        // A folder of that name holds the whole program or nothing, unless a hand took the program out
        // of it: that one goes, or the rename below could never land. The program is asked again
        // here, since another build may have renamed its folder into place since the line above,
        // and that folder is whole.
        if (Directory.Exists(folder) && !File.Exists(program))
        {
            try
            {
                Directory.Delete(folder, recursive: true);
            }
            catch (DirectoryNotFoundException)
            {
                // Another build took the same broken folder away first.
            }
        }

        var staging = folder + ".partial-" + Guid.NewGuid().ToString("N");
        Directory.CreateDirectory(staging);
        try
        {
            using (var zip = ZipFile.OpenRead(archive))
            {
                var entry = zip.GetEntry(name)
                    ?? throw new InvalidDataException($"{archive} holds no entry named {name}.");
                entry.ExtractToFile(Path.Combine(staging, name));
            }
            MakeExecutable(Path.Combine(staging, name));

            try
            {
                Directory.Move(staging, folder);
            }
            catch (IOException) when (File.Exists(program))
            {
                // Another build unpacked the same zip first, and its folder is the same bytes.
                return (program, false);
            }
            return (program, true);
        }
        finally
        {
            if (Directory.Exists(staging)) Directory.Delete(staging, recursive: true);
        }
    }

    /// <summary>The SHA-256 of <paramref name="file"/>, in lowercase hexadecimal.</summary>
    private static string Digest(string file)
    {
        using var sha = SHA256.Create();
        using var stream = File.OpenRead(file);
        var hex = new StringBuilder(64);
        foreach (var b in sha.ComputeHash(stream)) hex.Append(b.ToString("x2"));
        return hex.ToString();
    }

    /// <summary>Through the system's own call, as <see cref="LinkDirectory"/> links a folder: a zip carries no mode MSBuild applies.</summary>
    private static void MakeExecutable(string file)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return;
        if (chmod(file, Executable) != 0) throw new Win32Exception(Marshal.GetLastWin32Error(), $"{file} could not be made executable");
    }

    [DllImport("libc", SetLastError = true)]
    private static extern int chmod(string path, int mode);
}
