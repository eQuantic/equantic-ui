using System.Linq;
using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using eQuantic.UI.Compiler.CodeGen;
using eQuantic.UI.Compiler.Models;
using eQuantic.UI.Compiler.Services;
using Xunit;

namespace eQuantic.UI.Compiler.Tests.Diagnostics;

/// <summary>
/// Front 4 — "fail on unsupported": constructs that cannot be transpiled now raise an error diagnostic
/// (and fail the build) instead of being emitted verbatim. Two layers are covered: the syntactic
/// <see cref="eQuantic.UI.Compiler.CodeGen.Strategies.Special.UnsupportedConstructStrategy"/> (EQ2001)
/// and the symbol-level <see cref="SemanticValidator"/> client/server boundary (EQ21xx).
/// </summary>
public class FailOnUnsupportedTests
{
    [Theory]
    [InlineData("__makeref(Id)")]   // typed-reference intrinsic — no JS equivalent
    [InlineData("__reftype(Id)")]
    public void ImpossibleConstruct_RaisesError(string expression)
    {
        var diagnostics = TestHelper.DiagnosticsFor(expression);

        diagnostics.Should().ContainSingle(d => d.Severity == ConversionSeverity.Error)
            .Which.Code.Should().Be("EQ2001");
    }

    [Fact]
    public void Goto_RaisesError()
    {
        // goto has no JS equivalent — must be a build error, not emitted verbatim.
        TestHelper.DiagnosticsFor("goto done")
            .Should().ContainSingle(d => d.Severity == ConversionSeverity.Error)
            .Which.Code.Should().Be("EQ2002");
    }

    [Fact]
    public void NoStrategyConstruct_RaisesGenericError_NotAVerbatimWarning()
    {
        // A construct the compiler has no strategy for (LINQ query syntax) must be a BUILD ERROR with a
        // source location, never a warning + verbatim C# that becomes `X is not defined` at runtime —
        // the generic no-strategy fallback (EQ1001/1002/1003) is now an error like the dedicated ones.
        TestHelper.DiagnosticsFor("from x in items select x")
            .Should().Contain(d => d.Severity == ConversionSeverity.Error
                && (d.Code == "EQ1001" || d.Code == "EQ1002" || d.Code == "EQ1003"));
    }

    [Fact]
    public void UnsafeBlock_WithoutPointers_Unwraps_NoError()
    {
        // `unsafe` is just a context marker; plain code inside transpiles normally (no diagnostic).
        var js = TestHelper.ConvertStatement("unsafe { int n = 1; }");
        js.Should().Contain("n = 1");
        TestHelper.DiagnosticsFor("unsafe { int n = 1; }").Should().BeEmpty();
    }

    [Theory]
    [InlineData("1 + 2 * 3")]
    [InlineData("items.Where(x => x.Length > 0).Select(x => x.Trim())")]
    [InlineData("Active ? \"yes\" : \"no\"")]
    [InlineData("str.Substring(0, 3)")]
    public void SupportedConstruct_RaisesNoDiagnostics(string expression)
    {
        // Guards against the verbatim-fallback firing on ordinary code (false positives).
        TestHelper.DiagnosticsFor(expression).Should().BeEmpty();
    }

    [Theory]
    [InlineData("System.Net.Http", "EQ2103", "new System.Net.Http.HttpClient().GetStringAsync(\"x\")")]
    [InlineData("System.IO", "EQ2101", "System.IO.File.ReadAllText(\"x\")")]
    public void ForbiddenApi_InClientComponent_RaisesBoundaryError(string _, string expectedCode, string call)
    {
        var (semanticModel, tree) = CompileClientComponentCalling(call);
        var component = new ComponentDefinition { IsStateful = true, SyntaxTree = tree, SourcePath = "Test.cs" };

        var errors = new SemanticValidator(semanticModel).Validate(component);

        errors.Should().Contain(e => e.Code == expectedCode);
    }

    [Fact]
    public void TaskDelay_IsNotForbidden()
    {
        // System.Threading.Tasks is supported (async maps to Promise) — only OS threading is forbidden.
        var (semanticModel, tree) = CompileClientComponentCalling("System.Threading.Tasks.Task.Delay(10)");
        var component = new ComponentDefinition { IsStateful = true, SyntaxTree = tree, SourcePath = "Test.cs" };

        new SemanticValidator(semanticModel).Validate(component).Should().BeEmpty();
    }

    private static (SemanticModel, SyntaxTree) CompileClientComponentCalling(string call)
    {
        var tree = CSharpSyntaxTree.ParseText($@"
            using System;
            public class C {{
                public async void M() {{ {call}; }}
            }}");

        var compilation = CSharpCompilation.Create("T", new[] { tree }, new[]
        {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(System.Net.Http.HttpClient).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(System.IO.File).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(System.Threading.Tasks.Task).Assembly.Location),
            MetadataReference.CreateFromFile(System.Reflection.Assembly.Load("System.Runtime").Location),
        });

        return (compilation.GetSemanticModel(tree), tree);
    }

    [Theory]
    [InlineData("System.Linq.Enumerable.Range(1, 3)")]
    [InlineData("System.Guid.NewGuid().ToByteArray()")]
    [InlineData("System.Text.Encoding.UTF8.GetBytes(\"x\")")]
    public void UntranslatedFrameworkCall_RaisesError(string expression)
    {
        // The fallback used to emit `Enumerable.range(1, 3)` and let the browser explain it with
        // "Enumerable is not defined". The file and line only exist at BUILD time — so does the fix.
        TestHelper.DiagnosticsFor(expression)
            .Should().Contain(d => d.Severity == ConversionSeverity.Error && d.Code == "EQ2004");
    }

    [Theory]
    [InlineData("Id.Length")]                       // a property, not a call
    [InlineData("Id.ToUpperInvariant()")]           // string methods ARE mapped
    [InlineData("System.Math.Max(1, 2)")]           // as are the Math ones
    public void TranslatedOrHarmlessCall_RaisesNothing(string expression)
    {
        TestHelper.DiagnosticsFor(expression)
            .Should().NotContain(d => d.Code == "EQ2004");
    }

    [Theory]
    [InlineData("Tags.Contains(\"a\")")]
    [InlineData("Tags?.Contains(\"a\")")]
    public void ContainsTranslates_WithOrWithoutTheQuestionMark(string expression)
    {
        // The `?.` shape used to be a dead end: the member sits in a MemberBindingExpression, no
        // strategy destructured it, and `Tags?.contains("a")` — a method no JS array has — only
        // failed once a browser ran it. Both shapes name the same call.
        var js = TestHelper.ConvertExpression(expression, "System.Collections.Generic.IReadOnlyCollection<string>");

        js.Should().Contain(".includes(");
        js.Should().NotContain(".contains(");
    }

    [Fact]
    public void NullConditionalContains_KeepsItsGuard()
    {
        TestHelper.ConvertExpression("Tags?.Contains(\"a\")",
                "System.Collections.Generic.IReadOnlyCollection<string>")
            .Should().Contain("?.", "the receiver may be null — that is the whole point of the operator");
    }
}
