using System.Reflection;

namespace eQuantic.UI.Web.Tests;

/// <summary>
/// The surface class a shipping assembly says it has (#899): <c>contract</c>, what the
/// compatibility promise covers, <c>tool</c>, the SDK's own tools, or <c>experimental</c>, the native
/// track. <c>Directory.Build.targets</c> derives it and writes it into the assembly as
/// <c>[assembly: AssemblyMetadata("eQuantic.Surface", …)]</c>, so it is read from what ships.
/// </summary>
internal static class SurfaceClass
{
    public const string Key = "eQuantic.Surface";
    public const string Contract = "contract";
    public const string Tool = "tool";
    public const string Experimental = "experimental";

    /// <summary>The class <paramref name="assembly"/> says it has, read without running any of its
    /// code, or nothing when it says none.</summary>
    public static string? Of(Assembly assembly) =>
        assembly.GetCustomAttributesData()
            .Where(attribute => attribute.AttributeType.FullName == typeof(AssemblyMetadataAttribute).FullName)
            .Select(attribute => attribute.ConstructorArguments)
            .Where(arguments => arguments.Count == 2 && arguments[0].Value as string == Key)
            .Select(arguments => arguments[1].Value as string)
            .FirstOrDefault();

    /// <summary>
    /// The shipping assemblies this test run carries, which are the ones a project of <c>src/</c>
    /// declares a public API for: derived from the run's own output, never listed, so an assembly the
    /// tests come to reference is read the day it arrives.
    /// </summary>
    public static IReadOnlyList<Assembly> ShippingAssemblies()
    {
        var root = SdkProbe.RepoRoot();
        return Directory.GetFiles(AppContext.BaseDirectory, "eQuantic.UI.*.dll")
            .Where(path => File.Exists(Path.Combine(root, "src", Path.GetFileNameWithoutExtension(path), "PublicAPI.Shipped.txt")))
            .OrderBy(path => path, StringComparer.Ordinal)
            .Select(path => Assembly.Load(AssemblyName.GetAssemblyName(path)))
            .ToList();
    }
}
