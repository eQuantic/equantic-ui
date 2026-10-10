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
}
