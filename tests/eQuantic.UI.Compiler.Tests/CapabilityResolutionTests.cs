using eQuantic.UI.Compiler;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace eQuantic.UI.Compiler.Tests;

/// <summary>
/// <c>context.GetService&lt;T&gt;()</c> — a capability asked for from the MIDDLE of a tree — has to
/// arrive on the other side still saying WHICH capability.
/// <para>
/// It did not. The strategy that turns the call into a key recognised the older Core
/// <c>RenderContext</c> and an <c>IServiceProvider</c>, and the write-once
/// <c>ComponentContext</c> is neither — so the call fell through to the ordinary invocation path,
/// which drops type arguments, and every web page resolved <c>getService()</c> with no key at all.
/// The feature shipped and then did nothing on the target it exists to serve.
/// </para>
/// </summary>
public class CapabilityResolutionTests
{
    private static string TypeScriptOf(string source)
    {
        var tree = CSharpSyntaxTree.ParseText(source, path: "Probe.cs");
        var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator)
            .Where(p => p.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
            .Select(p => (MetadataReference)MetadataReference.CreateFromFile(p))
            .Append(MetadataReference.CreateFromFile(typeof(eQuantic.UI.Primitives.VisualNode).Assembly.Location));
        var compilation = CSharpCompilation.Create("Probe", [tree], references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary,
                nullableContextOptions: NullableContextOptions.Enable));

        var compiler = new ComponentCompiler();
        compiler.SetProjectCompilation(compilation);
        var result = compiler.CompileSource(source, "Probe.cs").Single();
        Assert.True(result.Success, string.Join("\n", result.Errors.Select(e => e.Message)));
        return result.TypeScript;
    }

    private const string Source = """
        using eQuantic.UI.Primitives;

        namespace Demo;

        public sealed class Copyable : StatelessComponent
        {
            public override VisualNode Build(ComponentContext context)
            {
                var clipboard = context.GetService<ITextClipboard>();
                return new Text(clipboard == null ? "no" : "yes", TypeRole.BodyM);
            }
        }
        """;

    [Fact]
    public void GetService_KeepsTheTypeItWasAskedFor()
    {
        var typeScript = TypeScriptOf(Source);

        Assert.Contains("getService('ITextClipboard')", typeScript);
        // The shape that shipped: a call with nothing in it always answers undefined.
        Assert.DoesNotContain("getService()", typeScript);
    }

    /// <summary>The playground's mode — no references, so nothing RESOLVES. The type argument is
    /// still right there in the source, and dropping it there costs the same page the same way.</summary>
    [Fact]
    public void GetService_KeepsTheType_WithoutReferencesToo()
    {
        var typeScript = new ComponentCompiler().CompileSource(Source, "Probe.cs").Single().TypeScript;

        Assert.Contains("getService('ITextClipboard')", typeScript);
        Assert.DoesNotContain("getService()", typeScript);
    }
}
