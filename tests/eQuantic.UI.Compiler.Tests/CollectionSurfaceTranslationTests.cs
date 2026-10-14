using System.Linq;
using Xunit;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using eQuantic.UI.Compiler.CodeGen;

namespace eQuantic.UI.Compiler.Tests;

/// <summary>
/// <c>Queue&lt;T&gt;</c>, <c>Stack&lt;T&gt;</c>, <c>LinkedList&lt;T&gt;</c>, <c>LinkedListNode&lt;T&gt;</c> and
/// <c>SortedSet&lt;T&gt;</c> cross as the runtime's twins: a member a twin carries is that twin's, and any
/// other is refused at the build (EQ2004), where every member went out under its camel-cased name and
/// failed only when the browser called it (#866). What each one DOES is held against .NET by the
/// conformance suite (CollectionSurfaceConformanceTests); this pins the refusals, which never run, and
/// the shapes a run cannot tell apart.
/// </summary>
public class CollectionSurfaceTranslationTests
{
    private readonly CSharpToJsConverter _converter = new();

    private (string Js, IReadOnlyList<string> Errors) Convert(string bodyCode)
    {
        var classCode = "using System; using System.Collections.Generic;\n"
            + $"class Wrapper {{ void Method(Queue<int> q, Stack<int> t, LinkedList<int> l, SortedSet<int> s) {{ {bodyCode} }} }}";
        var tree = CSharpSyntaxTree.ParseText(classCode);
        var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator)
            .Where(path => path.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
            .Select(path => (MetadataReference)TestReferences.Of(path));
        var compilation = CSharpCompilation.Create("probe", [tree], references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));
        _converter.SetSemanticModel(compilation.GetSemanticModel(tree));
        _converter.ClearDiagnostics();

        var method = tree.GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>().First();
        var js = _converter.Convert(method.Body!).Trim();
        var errors = _converter.Diagnostics
            .Where(diagnostic => diagnostic.Severity == ConversionSeverity.Error)
            .Select(diagnostic => diagnostic.Code)
            .ToList();
        return (js, errors);
    }

    [Theory]
    [InlineData("var c = s.Comparer;")]
    [InlineData("var c = SortedSet<int>.CreateSetComparer();")]
    [InlineData("var e = q.GetEnumerator();")]
    [InlineData("var e = t.GetEnumerator();")]
    [InlineData("var e = l.GetEnumerator();")]
    [InlineData("var e = s.GetEnumerator();")]
    [InlineData("l.OnDeserialization(null);")]
    [InlineData("int v = l.First!.ValueRef;")]
    public void AMemberNoTwinCarries_IsRefusedAtTheBuild(string body)
    {
        var (_, errors) = Convert(body);

        Assert.Contains("EQ2004", errors);
    }

    [Fact]
    public void ANodeOverload_CallsTheTwinNamedForTheNode()
    {
        var (js, errors) = Convert(
            "var n = new LinkedListNode<int>(1); l.AddFirst(n); l.Remove(n); l.AddLast(n); l.AddAfter(n, new LinkedListNode<int>(2)); l.AddBefore(n, 3); l.Remove(3);");

        Assert.Empty(errors);
        Assert.Contains("$eq.collections.linkedListNode(1)", js);
        Assert.Contains("l.addNodeFirst(n)", js);
        Assert.Contains("l.removeNode(n)", js);
        Assert.Contains("l.addNodeLast(n)", js);
        Assert.Contains("l.addNodeAfter(n, $eq.collections.linkedListNode(2))", js);
        Assert.Contains("l.addBefore(n, 3)", js);
        Assert.Contains("l.remove(3)", js);
    }

    [Fact]
    public void AMethodGroup_KeepsItsReceiver()
    {
        var (js, errors) = Convert("var items = new List<int> { 1 }; items.ForEach(q.Enqueue); Action<int> push = t.Push;");

        Assert.Empty(errors);
        Assert.Contains("q.enqueue.bind(q)", js);
        Assert.Contains("t.push.bind(t)", js);
    }

    [Fact]
    public void ATakeAsAMethodGroup_IsRefused()
    {
        var (_, errors) = Convert("TryTake take = q.TryDequeue; } delegate bool TryTake(out int value); void Other() {");

        Assert.Contains("EQ2004", errors);
    }

    [Fact]
    public void AnObjectMember_IsNotTheTwins()
    {
        // `ToString`, `Equals` and the hash code are every object's, translated where every object's are.
        var (js, _) = Convert("var text = q.ToString();");

        Assert.DoesNotContain("q.toString()", js);
    }
}
