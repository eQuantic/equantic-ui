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
}
