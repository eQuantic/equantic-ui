namespace SharedLibrary.Common;

/// <summary>
/// The line the solution's apps end a page with. It is INTERNAL: it is this solution's own business,
/// and the library opens it to the web app by name, with an <c>InternalsVisibleTo</c> item in its
/// project file. That item names the app's ASSEMBLY, which the web app's project file names apart
/// from itself, so this record reaches the browser only when the build knows the app by the name
/// MSBuild evaluated.
/// </summary>
internal sealed record Imprint(string Edition)
{
    public string Line => $"Shared library, {Edition} edition";
}
