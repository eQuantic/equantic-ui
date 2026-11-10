using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using eQuantic.UI.Compiler.CodeGen;
using eQuantic.UI.Compiler.Services;
using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;

namespace eQuantic.UI.Compiler.Tests.Coverage;

/// <summary>
/// A C# local or parameter named like a global the emitted code reads is renamed, so it cannot hide
/// that global from a lowering in its scope: <c>var crypto = "xy";</c> beside <c>Guid.NewGuid()</c>
/// threw (<c>crypto.randomUUID is not a function</c>), and <c>int undefined = 5;</c> filled the
/// parameter a named argument skipped (#397). The globals are read from the compiler's own source,
/// every one a string it writes calls, reads a member of, or is, so a lowering that starts reading
/// another one is covered the day it does.
/// </summary>
public class EmittedGlobalsTests
{
    /// <summary>The globals of JavaScript and the browser, the universe the scan below picks from.</summary>
    private static readonly string[] PlatformGlobals =
    [
        "globalThis", "undefined", "isNaN", "isFinite", "parseInt", "parseFloat", "encodeURI", "decodeURI",
        "encodeURIComponent", "decodeURIComponent", "escape", "unescape", "console", "window", "document",
        "navigator", "location", "history", "localStorage", "sessionStorage", "fetch", "setTimeout",
        "clearTimeout", "setInterval", "clearInterval", "queueMicrotask", "structuredClone",
        "requestAnimationFrame", "cancelAnimationFrame", "crypto", "performance", "atob", "btoa",
        "alert", "confirm", "prompt", "self",
        "Array", "ArrayBuffer", "BigInt", "Boolean", "DataView", "Date", "Error", "Function", "Infinity",
        "Intl", "JSON", "Map", "Math", "NaN", "Number", "Object", "Promise", "Proxy", "RangeError", "Reflect",
        "RegExp", "Set", "String", "Symbol", "TextDecoder", "TextEncoder", "TypeError", "URL", "WeakMap",
        "WeakSet", "Int8Array", "Uint8Array", "Int16Array", "Uint16Array", "Int32Array", "Uint32Array",
        "Float32Array", "Float64Array", "BigInt64Array", "BigUint64Array",
    ];

    private static string RepoRoot([CallerFilePath] string sourcePath = "") =>
        Path.GetFullPath(Path.Combine(Path.GetDirectoryName(sourcePath)!, "..", "..", ".."));

    private static IReadOnlyList<string> GlobalsTheCompilerWrites()
    {
        var found = new SortedSet<string>(StringComparer.Ordinal);
        var codeGen = Path.Combine(RepoRoot(), "src", "eQuantic.UI.Compiler", "CodeGen");
        foreach (var file in Directory.EnumerateFiles(codeGen, "*.cs", SearchOption.AllDirectories))
        {
            var root = CSharpSyntaxTree.ParseText(File.ReadAllText(file), ParseDefaults.Options).GetRoot();
            foreach (var token in root.DescendantTokens())
            {
                if (!token.IsKind(SyntaxKind.StringLiteralToken) && !token.IsKind(SyntaxKind.InterpolatedStringTextToken)
                    && !token.IsKind(SyntaxKind.SingleLineRawStringLiteralToken)
                    && !token.IsKind(SyntaxKind.MultiLineRawStringLiteralToken))
                    continue;
                foreach (var global in PlatformGlobals)
                    if (token.ValueText == global
                        || Regex.IsMatch(token.ValueText, $@"(?<![\w$.'""]){global}\s*(\(|\.[A-Za-z_$]|\[)"))
                        found.Add(global);
            }
        }
        return found.ToList();
    }

    public static TheoryData<string> GlobalsTheCompilerEmits()
    {
        var data = new TheoryData<string>();
        foreach (var global in GlobalsTheCompilerWrites()) data.Add(global);
        return data;
    }

    [Fact]
    public void TheScan_FindsTheGlobalsItIsKnownToWrite()
    {
        // The instrument first: a scan that read nothing would leave the theory below with no case.
        GlobalsTheCompilerWrites().Should().Contain(["console", "crypto", "undefined", "Math", "Number"]);
    }

    [Theory]
    [MemberData(nameof(GlobalsTheCompilerEmits))]
    public void ALocalNamedLikeAGlobalTheOutputReads_IsRenamed(string global)
    {
        global.ToJsIdentifier().Should().Be(global + "$",
            $"a C# local called {global} would hide the global every lowering in its scope reads");
    }

    /// <summary>
    /// No type the runtime ships is named like a word JavaScript reserves or a global the emitted code
    /// reads. A reference renames such a type's twin (<c>Map$</c>, #934, #890), while the runtime's index
    /// exports its types under the names C# gives them, so a type of the component library named
    /// <c>Map</c> would be imported under a name the runtime does not export.
    /// </summary>
    [Fact]
    public void NoTypeTheRuntimeShips_IsNamedLikeAGlobalOrAReservedWord()
    {
        var shipped = Directory.GetFiles(AppContext.BaseDirectory, "eQuantic.UI.*.dll")
            .Select(path =>
            {
                try { return Assembly.LoadFrom(path); }
                catch (BadImageFormatException) { return null; }
            })
            .OfType<Assembly>()
            .SelectMany(assembly =>
            {
                try { return assembly.GetTypes(); }
                catch (ReflectionTypeLoadException exception) { return exception.Types.OfType<Type>().ToArray(); }
            })
            .Where(type => type is { IsNested: false, Namespace: { } ns } && (type.IsClass || type is { IsValueType: true, IsEnum: false })
                && RuntimeProvidedTypeScanner.IsTranspiledNamespace(ns))
            .Select(type => type.Name.Split('`')[0])
            .ToList();

        shipped.Should().Contain("Button", "the component library is among the assemblies read");
        shipped.Where(name => name.ToJsIdentifier() != name).Should().BeEmpty(
            "a reference would name such a type's twin with a $ the runtime does not export");
    }
}
