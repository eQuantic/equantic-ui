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
    /// <para>
    /// The rule itself is <see cref="eQuantic.UI.TwinName"/>, shared with the source generator, whose
    /// hydration manifest names what the server sends by the name this gives the twin's member.
    /// </para>
    /// </summary>
    public static string ToCamelCase(this string name) => eQuantic.UI.TwinName.Of(name);

    /// <summary>
    /// A C# LOCAL/PARAMETER name as a legal JS identifier (<see cref="eQuantic.UI.JsNames.Untaken"/>):
    /// the verbatim escape comes off first (`@checked` → `checked` — the `@` is C#'s keyword escape and
    /// a syntax error in JS), then a reserved word, or a global the emitted code reads, takes a
    /// trailing `$` (`package` → `package$`, `crypto` → `crypto$`). Renaming must be applied
    /// at BOTH the declaration and every reference — which is why it lives here, on the one path
    /// both go through. Anything else passes back unchanged, so ordinary names read as authored.
    /// <para>
    /// A `$` because no C# identifier can hold one, so the renamed name cannot land on another
    /// name in its scope. The underscore it took before could: `package` and `package_` in one
    /// scope were both `package_`, and the module did not parse.
    /// </para>
    /// </summary>
    public static string ToJsIdentifier(this string name) => eQuantic.UI.JsNames.Untaken(name);
}
