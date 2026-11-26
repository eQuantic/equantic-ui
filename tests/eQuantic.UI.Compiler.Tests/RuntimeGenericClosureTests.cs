using System.Reflection;
using eQuantic.UI.Compiler.CodeGen;
using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;

namespace eQuantic.UI.Compiler.Tests;

/// <summary>
/// A closed construction of a generic the runtime library twins is marked with its type arguments where an
/// app writes it, as one the source declares is (#1863). An app reads the framework from METADATA, so the
/// library here is compiled apart and referenced by its image, as the app's build references the
/// framework's assemblies: `new GestureRecognizerFactoryWithHandlers&lt;HoldThenDrag&gt;(…)` crossed unmarked,
/// and the browser took two factories over different types for one runtime type.
/// </summary>
public class RuntimeGenericClosureTests
{
    /// <summary>A library of two generics: one in a namespace the runtime twins, one in a namespace it does not.</summary>
    private const string Library = """
        namespace eQuantic.UI.Components
        {
            public sealed class Factory<T> { public Factory(int value) { } }
            public struct Pair<T> { public T Value; }
        }
        namespace Acme.Elsewhere
        {
            public sealed class Box<T> { public Box(int value) { } }
        }
        """;

    private static MetadataReference[] Framework() =>
    [
        TestReferences.Of(typeof(object).Assembly.Location),
        TestReferences.Of(Assembly.Load("System.Runtime").Location),
    ];

    /// <summary>The library's image, referenced as metadata: no symbol of it is in the app's source.</summary>
    private static MetadataReference LibraryImage()
    {
        var library = CSharpCompilation.Create("RuntimeLibrary", [CSharpSyntaxTree.ParseText(Library)], Framework(),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        using var image = new MemoryStream();
        var emitted = library.Emit(image);
        emitted.Success.Should().BeTrue(string.Join("\n", emitted.Diagnostics));
        return MetadataReference.CreateFromImage(image.ToArray());
    }

    /// <summary>The JavaScript an app's expression becomes, the library read from its image.</summary>
    private static string Convert(string expression)
    {
        var tree = CSharpSyntaxTree.ParseText($$"""
            public sealed class HoldThenDrag { }
            public class Sample { public void Method() { var x = {{expression}}; } }
            """);
        var compilation = CSharpCompilation.Create("App", [tree], [.. Framework(), LibraryImage()],
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        compilation.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).Should().BeEmpty();
        var converter = new CSharpToJsConverter();
        converter.SetSemanticModel(compilation.GetSemanticModel(tree));
        var value = tree.GetRoot().DescendantNodes().OfType<VariableDeclaratorSyntax>().Single().Initializer!.Value;
        return converter.ConvertExpression(value);
    }

    [Fact]
    public void ARuntimeGeneric_ReadFromMetadata_IsMarkedWithItsTypeArguments()
    {
        Convert("new eQuantic.UI.Components.Factory<HoldThenDrag>(1)")
            .Should().Contain("$eq.closing(").And.Contain("'global::HoldThenDrag'");
        Convert("new eQuantic.UI.Components.Factory<int>(1)").Should().Contain("$eq.closing(").And.Contain("'int'");
        Convert("new eQuantic.UI.Components.Pair<string>()").Should().Contain("$eq.closing(").And.Contain("'string'");
    }

    [Fact]
    public void ARuntimeGenericsTypeOf_IsClosedOverTheSameText()
    {
        // What a marked value's GetType() is compared with: `typeof` of the closed type, by the same text.
        var compared = Convert(
            "new eQuantic.UI.Components.Factory<int>(1).GetType() == typeof(eQuantic.UI.Components.Factory<string>)");
        compared.Should().Contain("$eq.closing(").And.Contain("'int'");
        compared.Should().Contain("$eq.types.named(Factory, 'string')");
    }

    [Fact]
    public void AGenericNeitherTheSourceNorTheRuntimeDeclares_StaysUnmarked()
    {
        Convert("new Acme.Elsewhere.Box<int>(1)").Should().NotContain("closing");
        Convert("new System.Collections.Generic.List<int>()").Should().NotContain("closing");
    }
}
