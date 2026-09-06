using System.Reflection;
using eQuantic.UI.Primitives;
using Microsoft.Win32;

namespace eQuantic.UI.Native.Shell.Windows;

/// <summary>
/// The registry, under <c>HKCU\Software\&lt;Company&gt;\&lt;Product&gt;</c> — the store a Windows
/// app has meant by "my settings" for thirty years, per user, roaming with the profile, visible in
/// every tool an administrator already owns. The Windows twin of <c>NSUserDefaults</c>, and the
/// same reason for it: nothing invented, and preferences end up where the platform's tooling
/// expects to find them.
/// <para>
/// Company and Product are the assembly's own — the <c>&lt;Company&gt;</c> and <c>&lt;Product&gt;</c>
/// an app's project already states, which MSBuild stamps as attributes. When neither was stated
/// both default to the assembly name, and one level is enough.
/// </para>
/// </summary>
public sealed class WindowsAppStorage : IAppStorage
{
    private readonly string _keyPath;

    public WindowsAppStorage() : this(AppIdentity.RegistryPath()) { }

    /// <summary>A store under an explicit key — what a test uses so it never touches the app's own.</summary>
    public WindowsAppStorage(string keyPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(keyPath);
        _keyPath = keyPath;
    }

    public string? Get(string key)
    {
        ArgumentException.ThrowIfNullOrEmpty(key);
        try
        {
            using var store = Registry.CurrentUser.OpenSubKey(_keyPath);
            return store?.GetValue(key) as string;
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
        try
        {
            using var store = Registry.CurrentUser.CreateSubKey(_keyPath);
            store.SetValue(key, value, RegistryValueKind.String);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            // A profile whose hive refuses the write — policy, a locked-down account — is a store
            // that keeps nothing, not a reason to take the app down: NSUserDefaults and
            // SharedPreferences never throw on a write either, and the next Get answers null.
        }
    }

    public void Remove(string key)
    {
        ArgumentException.ThrowIfNullOrEmpty(key);
        try
        {
            using var store = Registry.CurrentUser.OpenSubKey(_keyPath, writable: true);
            store?.DeleteValue(key, throwOnMissingValue: false);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            // Forgetting what was never there is not an error, and neither is a store nobody created.
        }
    }
}

/// <summary>Who this app is to the operating system's own stores, read off its assembly.</summary>
internal static class AppIdentity
{
    public static string Company =>
        Attribute<AssemblyCompanyAttribute>()?.Company is { Length: > 0 } company ? company : Product;

    public static string Product =>
        Attribute<AssemblyProductAttribute>()?.Product is { Length: > 0 } product
            ? product
            : Assembly.GetEntryAssembly()?.GetName().Name ?? "eQuantic.UI";

    /// <summary><c>Software\Company\Product</c>, or <c>Software\Product</c> when the two are one name.</summary>
    public static string RegistryPath()
    {
        var company = Sanitize(Company);
        var product = Sanitize(Product);
        return string.Equals(company, product, StringComparison.OrdinalIgnoreCase)
            ? @"Software\" + product
            : @"Software\" + company + @"\" + product;
    }

    /// <summary>The per-user local data folder for this app — where a vault or a cache belongs.</summary>
    public static string LocalDataPath()
    {
        var company = Sanitize(Company);
        var product = Sanitize(Product);
        var root = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return string.Equals(company, product, StringComparison.OrdinalIgnoreCase)
            ? Path.Combine(root, product)
            : Path.Combine(root, company, product);
    }

    private static T? Attribute<T>() where T : Attribute =>
        Assembly.GetEntryAssembly()?.GetCustomAttribute<T>();

    private static string Sanitize(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var cleaned = new string(name.Select(c => invalid.Contains(c) ? '_' : c).ToArray()).Trim();
        return cleaned.Length > 0 ? cleaned : "eQuantic.UI";
    }
}
