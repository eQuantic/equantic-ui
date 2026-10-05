using System.Linq;
using Xunit;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using eQuantic.UI.Compiler.CodeGen;

namespace eQuantic.UI.Compiler.Tests;

/// <summary>
/// The cancellation trio crosses as the runtime's (<c>utils/cancellation.ts</c>): what C# builds is
/// built through <c>$eq.cancellation</c>, a member the twin carries is that member in camelCase, and
/// any other is refused at the build (EQ2004) instead of failing when the browser calls it. What each
/// one DOES is held against .NET by the conformance suite (CancellationConformanceTests); this pins
/// the shapes, and the refusals, which never run.
/// </summary>
public class CancellationTranslationTests
{
    private readonly CSharpToJsConverter _converter = new();

    private (string Js, IReadOnlyList<string> Errors) Convert(string bodyCode)
    {
        var classCode = "using System; using System.Threading;\n"
            + $"class Wrapper {{ CancellationTokenSource? field; void Method(CancellationToken given) {{ {bodyCode} }} }}";
        var tree = CSharpSyntaxTree.ParseText(classCode);
        var compilation = CSharpCompilation.Create("probe", [tree],
            [TestReferences.Of(typeof(object).Assembly.Location)],
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
    [InlineData("var s = new CancellationTokenSource();", "$eq.cancellation.source()")]
    [InlineData("field = new CancellationTokenSource(250);", "$eq.cancellation.source(250)")]
    [InlineData("var t = CancellationToken.None;", "$eq.cancellation.none")]
    [InlineData("CancellationToken t = default;", "$eq.cancellation.none")]
    [InlineData("var t = new CancellationToken(true);", "$eq.cancellation.token(true)")]
    [InlineData("var l = CancellationTokenSource.CreateLinkedTokenSource(given, CancellationToken.None);",
        "$eq.cancellation.linked(given, $eq.cancellation.none)")]
    public void WhatCSharpBuilds_IsBuiltByTheRuntime(string body, string expected)
    {
        var (js, errors) = Convert(body);

        Assert.Empty(errors);
        Assert.Contains(expected, js);
    }

    [Theory]
    [InlineData("var s = new CancellationTokenSource(); s.Cancel();", "s.cancel()")]
    [InlineData("field?.Cancel();", ".cancel()")]
    [InlineData("var a = given.IsCancellationRequested;", "given.isCancellationRequested")]
    [InlineData("var b = given.CanBeCanceled;", "given.canBeCanceled")]
    [InlineData("given.ThrowIfCancellationRequested();", "given.throwIfCancellationRequested()")]
    [InlineData("var r = given.Register(() => { }); r.Dispose();", "r.dispose()")]
    [InlineData("var s = new CancellationTokenSource(); var t = s.Token; s.CancelAfter(10);", "s.cancelAfter(10)")]
    public void AMemberTheTwinCarries_IsItsOwnInCamelCase(string body, string expected)
    {
        var (js, errors) = Convert(body);

        Assert.Empty(errors);
        Assert.Contains(expected, js);
    }

    [Theory]
    [InlineData("var s = new CancellationTokenSource(); s.Cancel(true);")]
    [InlineData("var s = new CancellationTokenSource(); var ok = s.TryReset();")]
    [InlineData("var h = given.WaitHandle;")]
    [InlineData("given.Register(state => { }, null);")]
    public void AnyOtherMember_IsRefusedAtTheBuild(string body)
    {
        var (_, errors) = Convert(body);

        Assert.Contains("EQ2004", errors);
    }
}
