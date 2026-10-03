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
            using static System.Console;
            using eQuantic.UI.Primitives;
            namespace App;
            public sealed class Logged : StatelessComponent
            {
                public override VisualNode Build(ComponentContext context)
                {
                    WriteLine("built");
                    return new Text("logged", TypeRole.BodyM);
                }
            }
            """, "Logged");

        result.Errors.Should().ContainSingle(error => error.Code == "EQ2004")
            .Which.Message.Should().Contain("System.Console.WriteLine").And.Contain("using static");
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
        // path and, since #485, on the bare one too.
        var result = Compile("""
            using static App.Wide;
            using eQuantic.UI.Primitives;
            namespace App;
            [System.Flags] public enum Wide : ulong { None = 0, Top = 1UL << 63 }
            public sealed class Widest : StatelessComponent
            {
                public override VisualNode Build(ComponentContext context)
                {
                    var both = Top | Wide.Top;
                    return new Text(both == Wide.None ? "none" : "some", TypeRole.BodyM);
                }
            }
            """, "Widest");

        result.Errors.Should().BeEmpty();
        result.TypeScript.Should().Contain("9223372036854775808 | 9223372036854775808");
    }

    private static CompilationResult Compile(string source, string component)
    {
        var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator)
            .Where(p => p.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
            .Select(p => (MetadataReference)MetadataReference.CreateFromFile(p))
            .Append(MetadataReference.CreateFromFile(typeof(eQuantic.UI.Primitives.VisualNode).Assembly.Location))
            .Append(MetadataReference.CreateFromFile(typeof(eQuantic.UI.Components.CodeEditor).Assembly.Location));
        var compilation = CSharpCompilation.Create("App",
            [CSharpSyntaxTree.ParseText(source, eQuantic.UI.Compiler.Services.ParseDefaults.Options, component + ".cs")],
            references, new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        compilation.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).Should().BeEmpty();
        var compiler = new ComponentCompiler();
        compiler.SetProjectCompilation(compilation);
        return compiler.CompileSource(source, component + ".cs").Single(r => r.ComponentName == component);
    }
}
