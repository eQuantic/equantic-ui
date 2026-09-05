using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using eQuantic.UI.Primitives;

namespace eQuantic.UI.Native.Shell.Windows;

/// <summary>
/// The Data Protection API — a secret encrypted with a key only THIS USER on THIS MACHINE can
/// unwrap, kept as one file per key under the app's own local data folder. What every browser on
/// Windows guards its cookie key with, and what .NET MAUI's secure storage is on Windows too.
/// <para>
/// DPAPI rather than the Credential Manager, deliberately: the Credential Manager is the visible
/// vault and the closer twin of the Keychain, but a generic credential's blob is capped at 2,560
/// bytes, and a JWT or a refresh token can pass that without warning — a store that silently
/// truncates a secret is worse than one the person cannot browse. Values here have no such limit,
/// are unreadable to any other account, and go with the app's data when it is removed.
/// </para>
/// <para>
/// Every failure answers rather than throws. A blob another user's profile wrote, a file a restore
/// brought back onto a different machine — the contract already says a null read means "sign in
/// again", which is the only useful response to any of them.
/// </para>
/// </summary>
public sealed unsafe class WindowsSecretStore : ISecretStore
{
    private const uint CRYPTPROTECT_UI_FORBIDDEN = 0x1;

    [StructLayout(LayoutKind.Sequential)]
    private struct DATA_BLOB
    {
        public uint Size;
        public byte* Data;
    }

    [DllImport("crypt32.dll", EntryPoint = "CryptProtectData", CharSet = CharSet.Unicode)]
    private static extern int CryptProtectData(DATA_BLOB* input, string? description, DATA_BLOB* entropy,
        IntPtr reserved, IntPtr prompt, uint flags, DATA_BLOB* output);

    [DllImport("crypt32.dll", EntryPoint = "CryptUnprotectData", CharSet = CharSet.Unicode)]
    private static extern int CryptUnprotectData(DATA_BLOB* input, IntPtr description, DATA_BLOB* entropy,
        IntPtr reserved, IntPtr prompt, uint flags, DATA_BLOB* output);

    [DllImport("kernel32.dll")]
    private static extern IntPtr LocalFree(IntPtr memory);

    private readonly string _directory;

    public WindowsSecretStore() : this(Path.Combine(AppIdentity.LocalDataPath(), "Secrets")) { }

    /// <summary>A vault in an explicit folder — what a test uses so it never touches the app's own.</summary>
    public WindowsSecretStore(string directory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        _directory = directory;
    }

    public string? Get(string key)
    {
        ArgumentException.ThrowIfNullOrEmpty(key);
        try
        {
            var path = PathFor(key);
            if (!File.Exists(path)) return null;
            var plain = Unprotect(File.ReadAllBytes(path));
            return plain is null ? null : Encoding.UTF8.GetString(plain);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    public void Set(string key, string value)
    {
        ArgumentException.ThrowIfNullOrEmpty(key);
        ArgumentNullException.ThrowIfNull(value);
        var protectedBytes = Protect(Encoding.UTF8.GetBytes(value))
            ?? throw new InvalidOperationException("The platform declined to protect the secret.");
        Directory.CreateDirectory(_directory);
        File.WriteAllBytes(PathFor(key), protectedBytes);
    }

    public void Remove(string key)
    {
        ArgumentException.ThrowIfNullOrEmpty(key);
        try
        {
            File.Delete(PathFor(key));
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            // Removing what is not there is not an error.
        }
    }

    /// <summary>The file a key lives in: a hash, so a key can be anything and the name is always legal.</summary>
    private string PathFor(string key) =>
        Path.Combine(_directory, Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key))) + ".bin");

    private static byte[]? Protect(byte[] plain)
    {
        fixed (byte* data = plain)
        {
            var input = new DATA_BLOB { Size = (uint)plain.Length, Data = data };
            DATA_BLOB output;
            if (CryptProtectData(&input, null, null, IntPtr.Zero, IntPtr.Zero, CRYPTPROTECT_UI_FORBIDDEN, &output) == 0)
                return null;
            return Take(output);
        }
    }

    private static byte[]? Unprotect(byte[] wrapped)
    {
        fixed (byte* data = wrapped)
        {
            var input = new DATA_BLOB { Size = (uint)wrapped.Length, Data = data };
            DATA_BLOB output;
            if (CryptUnprotectData(&input, IntPtr.Zero, null, IntPtr.Zero, IntPtr.Zero, CRYPTPROTECT_UI_FORBIDDEN, &output) == 0)
                return null;
            return Take(output);
        }
    }

    /// <summary>Copies the API's LocalAlloc'd answer into managed memory and frees the original.</summary>
    private static byte[] Take(DATA_BLOB blob)
    {
        try
        {
            var bytes = new byte[blob.Size];
            new ReadOnlySpan<byte>(blob.Data, (int)blob.Size).CopyTo(bytes);
            return bytes;
        }
        finally
        {
            LocalFree((IntPtr)blob.Data);
        }
    }
}
