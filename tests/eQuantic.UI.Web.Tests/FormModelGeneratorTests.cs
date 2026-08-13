using eQuantic.UI.Generators;
using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace eQuantic.UI.Web.Tests;

/// <summary>
/// The bridge from a model's DataAnnotations to a form.
/// <para>
/// It exists because the obvious implementation cannot: DataAnnotations validates by REFLECTION,
/// which the transpiler cannot carry into a browser. So the attributes are read at BUILD time and
/// what is emitted is ORDINARY calls to the same <c>Rules</c> a hand-written form uses — which is
/// why the generated form and the hand-written one are the same object, and why a form can outgrow
/// the bridge without a rewrite.
/// </para>
/// </summary>
public class FormModelGeneratorTests
{
    private static (string Source, IReadOnlyList<Diagnostic> Diagnostics) Run(string appCode)
    {
        var tree = CSharpSyntaxTree.ParseText(appCode, path: "App.cs");
        var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator)
            .Where(p => p.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
            .Select(p => (MetadataReference)MetadataReference.CreateFromFile(p))
            .Append(MetadataReference.CreateFromFile(typeof(eQuantic.UI.Primitives.VisualNode).Assembly.Location));
        var compilation = CSharpCompilation.Create("App", [tree], references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary,
                nullableContextOptions: NullableContextOptions.Enable));

        var driver = CSharpGeneratorDriver.Create(new FormModelGenerator())
            .RunGeneratorsAndUpdateCompilation(compilation, out var updated, out _);
        var result = driver.GetRunResult().Results.Single();

        var generated = result.GeneratedSources.Length > 0
            ? result.GeneratedSources[0].SourceText.ToString()
            : "";
        // The generated form must COMPILE against the real Primitives — a generator that emits
        // plausible text nobody builds is a generator that is wrong at the worst moment.
        if (generated.Length > 0)
            updated.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error)
                .Should().BeEmpty();
        return (generated, result.Diagnostics);
    }

    private const string SignUp = """
        using System.ComponentModel.DataAnnotations;
        using eQuantic.UI.Primitives;

        namespace Demo;

        [FormModel]
        public sealed class SignUp
        {
            [Required, EmailAddress] public string Email { get; set; } = "";
            [Required, StringLength(64, MinimumLength = 8)] public string Password { get; set; } = "";
            [Compare(nameof(Password))] public string Confirm { get; set; } = "";
            [Range(18, 120, ErrorMessage = "You must be 18 or older.")] public int Age { get; set; }
            public string? Referral { get; set; }
        }
        """;

    [Fact]
    public void AModelBecomesAFormOfTheSameRulesAHandWrittenOneUses()
    {
        var (source, diagnostics) = Run(SignUp);

        diagnostics.Should().BeEmpty();
        source.Should().Contain("public static class SignUpForm");
        source.Should().Contain("""form.Add("Email", "", [Rules.Required(), Rules.Email()])""");
        // StringLength carries BOTH bounds, and the minimum only when there is one.
        source.Should().Contain("""form.Add("Password", "", [Rules.Required(), Rules.MinLength(8), Rules.MaxLength(64)])""");
        // A property with no annotations is still a FIELD: the form is the model's shape, and a
        // page that could not bind an optional box would be a bridge with a hole in it.
        source.Should().Contain("""form.Add("Referral", "")""");
    }

    /// <summary>The message the annotation carries wins over the rule's own — an app that wrote one
    /// wrote it to be shown.</summary>
    [Fact]
    public void TheAnnotationsMessageIsTheOneTheFieldSays()
    {
        var (source, _) = Run(SignUp);

        source.Should().Contain("""Rules.Range(18, 120, "You must be 18 or older.")""");
    }

    /// <summary>
    /// `[Compare]` is a CROSS-FIELD rule, and it goes on after every field exists — otherwise it
    /// would depend on the order the properties happen to be declared in, and a model that puts
    /// `Confirm` above `Password` would silently compare against nothing.
    /// </summary>
    [Fact]
    public void CompareIsWiredAfterEveryFieldExists()
    {
        var (source, _) = Run("""
            using System.ComponentModel.DataAnnotations;
            using eQuantic.UI.Primitives;

            namespace Demo;

            [FormModel]
            public sealed class Reversed
            {
                [Compare(nameof(Password))] public string Confirm { get; set; } = "";
                [Required] public string Password { get; set; } = "";
            }
            """);

        source.Should().Contain("confirmField.AddRule(Rules.Matches(passwordField));");
        source.IndexOf("confirmField.AddRule").Should()
            .BeGreaterThan(source.IndexOf("form.Add(\"Password\""),
                "the rule is added after the field it compares to exists");
    }

    /// <summary>A regular expression crosses as a rule the browser can run — the transpiler turns
    /// `Regex.IsMatch` into a `RegExp`, so the pattern the model wrote is the pattern enforced.</summary>
    [Fact]
    public void ARegularExpressionBecomesARuleAndStillPassesOnEmpty()
    {
        var (source, _) = Run("""
            using System.ComponentModel.DataAnnotations;
            using eQuantic.UI.Primitives;

            namespace Demo;

            [FormModel]
            public sealed class Coupon
            {
                [RegularExpression("^[A-Z]{4}$")] public string Code { get; set; } = "";
            }
            """);

        source.Should().Contain("Rules.Custom(");
        source.Should().Contain("""Regex.IsMatch(value, "^[A-Z]{4}$")""");
        source.Should().Contain("value.Length == 0 ||", "an optional box left blank is absent, not malformed");
    }

    /// <summary>
    /// What the bridge cannot carry, it SAYS. A silently missing field is the failure mode this
    /// whole design is trying to avoid — the server still enforces the rule, and the developer
    /// finds out at compile time rather than from a user.
    /// </summary>
    [Fact]
    public void WhatItCannotCarryIsReportedRatherThanDropped()
    {
        var (_, diagnostics) = Run("""
            using System;
            using System.ComponentModel.DataAnnotations;
            using eQuantic.UI.Primitives;

            namespace Demo;

            [FormModel]
            public sealed class Booking
            {
                [Required] public DateTime When { get; set; }
                [Phone] public string Phone { get; set; } = "";
                [Compare("Nope")] public string Confirm { get; set; } = "";
            }
            """);

        diagnostics.Select(d => d.Id).Should().BeEquivalentTo(["EQ3103", "EQ3104", "EQ3105"]);
        diagnostics.Should().OnlyContain(d => d.Severity == DiagnosticSeverity.Warning,
            "a form that carries most of a model is still worth having");
    }

    /// <summary>Opt-in: a model annotated for an API is not automatically a form, or every DTO in
    /// the project would become one.</summary>
    [Fact]
    public void AModelWithoutTheMarkerIsNotAForm()
    {
        var (source, diagnostics) = Run("""
            using System.ComponentModel.DataAnnotations;

            namespace Demo;

            public sealed class ApiDto
            {
                [Required] public string Name { get; set; } = "";
            }
            """);

        source.Should().BeEmpty();
        diagnostics.Should().BeEmpty();
    }
}
