namespace eQuantic.UI.Compiler.CodeGen;

/// <summary>
/// String extensions used across the code generator. Lives in the <c>CodeGen</c> namespace so every
/// strategy (nested under it) sees these without an extra <c>using</c>.
/// </summary>
public static class StringExtensions
{
    /// <summary>
    /// PascalCase C# identifier → camelCase JS identifier — the member/property casing the runtime
    /// expects (e.g. <c>FirstName</c> → <c>firstName</c>). Empty/null is returned unchanged.
    /// </summary>
    public static string ToCamelCase(this string name) =>
        string.IsNullOrEmpty(name) ? name : char.ToLowerInvariant(name[0]) + name[1..];
}
