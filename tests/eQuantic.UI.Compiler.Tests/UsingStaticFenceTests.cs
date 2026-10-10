using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace eQuantic.UI.Compiler.Tests;

/// <summary>
/// A .NET type's member reached bare through <c>using static</c> translates as its qualified spelling
/// does, or fails the build with EQ2004, never a guess (#485). The rule that names a static through its
/// class is for the types the transpiler emits, and it wrote <c>Console.writeLine(…)</c> and
/// <c>String.join(…)</c> for classes nothing defines, which compiled and threw in the browser alone.
/// </summary>
public class UsingStaticFenceTests
{
    [Fact]
    public void AFrameworkMethodNoStrategyClaims_FailsTheBuild()
    {
        var result = Compile("""
            using static System.Environment;
            using eQuantic.UI.Primitives;
            namespace App;
            public sealed class Logged : StatelessComponent
            {
                public override VisualNode Build(ComponentContext context) =>
                    new Text(GetEnvironmentVariable("HOME") ?? "none", TypeRole.BodyM);
            }
            """, "Logged");

        result.Errors.Should().ContainSingle(error => error.Code == "EQ2004")
            .Which.Message.Should().Contain("System.Environment.GetEnvironmentVariable").And.Contain("using static");
    }

    /// <summary>
    /// A member reached bare goes where its qualified spelling goes, so every strategy that claims the
    /// qualified form claims it too: <c>Now</c>, <c>NewGuid()</c> and <c>WriteLine(…)</c> failed the
    /// build with EQ2004 while <c>DateTime.Now</c>, <c>Guid.NewGuid()</c> and <c>Console.WriteLine(…)</c>
    /// translated, because their strategies match a member access only (#556).
    /// </summary>
    [Fact]
    public void AFrameworkMemberItsQualifiedSpellingTranslates_TranslatesBare()
    {
        var result = Compile("""
            using static System.Console;
            using static System.DateTime;
            using static System.Guid;
            using eQuantic.UI.Primitives;
            namespace App;
            public sealed class Stamped : StatelessComponent
            {
                public override VisualNode Build(ComponentContext context)
                {
                    WriteLine("built");
                    return new Text($"{Now.Year} {UtcNow.Month} {Today.Day} {NewGuid()} {Empty}", TypeRole.BodyM);
                }
            }
            """, "Stamped");

        result.Errors.Should().BeEmpty();
        result.TypeScript.Should().Contain("console.").And.Contain("dateTime.now()").And.Contain("dateTime.utcNow()")
            .And.Contain("dateTime.today()").And.Contain("crypto.randomUUID()").And.Contain("00000000-0000-0000-0000-000000000000")
            .And.NotContain("DateTime.").And.NotContain("Guid.").And.NotContain("Console.");
    }

    [Fact]
    public void AFrameworkMemberAStrategyClaims_Translates()
    {
        var result = Compile("""
            using static System.Math;
            using static System.String;
            using eQuantic.UI.Primitives;
            namespace App;
            public sealed class Sum : StatelessComponent
            {
                public override VisualNode Build(ComponentContext context) =>
                    new Text(Join(",", new[] { $"{Max(1, 2)}", $"{Round(PI)}" }) + Empty, TypeRole.BodyM);
            }
            """, "Sum");

        result.Errors.Should().BeEmpty();
        result.TypeScript.Should().NotContain("String.").And.NotContain("Math.pI").And.Contain("3.141592653589793");
    }

    [Fact]
    public void AFlagsMemberPastLongsRange_IsItsValue_BareAndQualified()
    {
        // Read through long, a ulong member past long's range crashed the compile, on the qualified
        // path and, since #485, on the bare one too. The value is written as it is. Combining such
        // members with `|` is a 64-bit operation JavaScript's own operator does not do, the same gap
        // a long flags enum's members past 32 bits have (#555), so this case only reads the member.
        var result = Compile("""
            using static App.Wide;
            using eQuantic.UI.Primitives;
            namespace App;
            [System.Flags] public enum Wide : ulong { None = 0, Top = 1UL << 63 }
            public sealed class Widest : StatelessComponent
            {
                public override VisualNode Build(ComponentContext context)
                {
                    var top = Top;
                    return new Text(top == Wide.Top ? "top" : "other", TypeRole.BodyM);
                }
            }
            """, "Widest");

        result.Errors.Should().BeEmpty();
        result.TypeScript.Should().Contain("let top = 9223372036854775808;").And.Contain("top === 9223372036854775808");
    }

    private static CompilationResult Compile(string source, string component)
    {
        var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator)
            .Where(p => p.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
            .Select(TestReferences.Of)
            .Append(TestReferences.Of(typeof(eQuantic.UI.Primitives.VisualNode)))
            .Append(TestReferences.Of(typeof(eQuantic.UI.Components.CodeEditor)));
        var compilation = CSharpCompilation.Create("App",
            [CSharpSyntaxTree.ParseText(source, eQuantic.UI.Compiler.Services.ParseDefaults.Options, component + ".cs")],
            references, new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        compilation.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).Should().BeEmpty();
        var compiler = new ComponentCompiler();
        compiler.SetProjectCompilation(compilation);
        return compiler.CompileSource(source, component + ".cs").Single(r => r.ComponentName == component);
    }
}
