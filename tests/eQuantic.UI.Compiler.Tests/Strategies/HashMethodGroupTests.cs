using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace eQuantic.UI.Compiler.Tests.Strategies;

/// <summary>
/// The method group <c>GetHashCode</c>, written bare inside a type, is a delegate over <c>this</c>'s
/// hash: it read <c>this.getHashCode.bind(this)</c>, and a class that does not override GetHashCode
/// has no <c>getHashCode</c> in its twin, so making the delegate threw (Copilot on #550). A class is
/// what the conformance prelude cannot run, so its emitted module is read here.
/// </summary>
public class HashMethodGroupTests
{
    [Fact]
    public void ABareGetHashCodeGroup_IsTheHashOfThis()
    {
        const string source = """
            using System;
            using eQuantic.UI.Primitives;
            namespace App;
            public sealed class Keys : StatelessComponent
            {
                public Func<int> Hasher() => GetHashCode;
                public Func<int> Through() => this.GetHashCode;
                public override VisualNode Build(ComponentContext context) => new Text($"{Hasher()()}", TypeRole.BodyM);
            }
            """;
        var compilation = CSharpCompilation.Create("App",
            [CSharpSyntaxTree.ParseText(source, eQuantic.UI.Compiler.Services.ParseDefaults.Options, "Keys.cs")],
            ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator)
                .Where(path => path.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
                .Select(TestReferences.Of)
                .Append(TestReferences.Of(typeof(eQuantic.UI.Primitives.VisualNode))),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        compilation.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).Should().BeEmpty();
        var compiler = new ComponentCompiler();
        compiler.SetProjectCompilation(compilation);
        var result = compiler.CompileSource(source, "Keys.cs").Single(r => r.ComponentName == "Keys");

        result.TypeScript.Should().NotContain("getHashCode.bind")
            .And.MatchRegex(@"hasher\(\)[^{]*\{\s*return \$eq\.hash\.group\(this\);")
            .And.MatchRegex(@"through\(\)[^{]*\{\s*return \$eq\.hash\.group\(this\);");
    }
}
