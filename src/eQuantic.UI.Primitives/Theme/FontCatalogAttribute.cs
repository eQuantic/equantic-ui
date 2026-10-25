namespace eQuantic.UI.Primitives;

/// <summary>
/// Names the type that holds an assembly's catalog of families: its public static
/// <see cref="FontFamily"/> properties.
/// <para>
/// The SDK writes it beside the catalog it generates from the files under an app's <c>Assets</c>
/// folder. The server reads it from the assemblies it scans and declares every family of the catalog
/// on the web, so a family a page names at a call site loads there as one the theme names does.
/// </para>
/// <para>
/// HOST ONLY: it marks an assembly, and no page names it.
/// </para>
/// </summary>
/// <param name="catalog">The type whose public static <see cref="FontFamily"/> properties are the catalog.</param>
[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
[ServerOnly]
public sealed class FontCatalogAttribute(Type catalog) : Attribute
{
    /// <summary>The type whose public static <see cref="FontFamily"/> properties are the catalog.</summary>
    public Type Catalog { get; } = catalog;
}
