using eQuantic.UI.Compiler.Tests.ReferencedProjects;
using FluentAssertions;

namespace eQuantic.UI.Compiler.Tests;

/// <summary>
/// A <c>[ServerOnly]</c> override of <c>ToString</c>, <c>Equals</c> or <c>GetHashCode</c> on a type that
/// crosses is refused where it is declared (#943). The twin leaves it out, and those are the members the
/// runtime calls on its own, for text and for equality, so the fence on a call never met them: a page
/// that showed <c>$"{receipt}"</c> built, and printed the type's name where .NET runs the override.
/// </summary>
public sealed class AServerOnlyOverrideTests
{
    private static string Member(string name) => name switch
    {
        "ToString" => "[ServerOnly] public override string ToString() => System.IO.File.ReadAllText(\"r.txt\");",
        "Equals" => "[ServerOnly] public override bool Equals(object? other) => System.IO.File.Exists(\"r.txt\");",
        "GetHashCode" => "[ServerOnly] public override int GetHashCode() => System.IO.File.ReadAllText(\"r.txt\").Length;",
        _ => throw new ArgumentOutOfRangeException(nameof(name), name, null),
    };

    private static string Type(string kind, string member) => kind switch
    {
        "a class" => $"public sealed class Receipt\n{{\n    public int Id = 7;\n    {member}\n}}",
        "a record" => $"public sealed record Receipt(int Id)\n{{\n    {member}\n}}",
        "a struct" => $"public struct Receipt\n{{\n    public int Id;\n    {member}\n}}",
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
    };

    private static Solution App(string kind, string member) => new Solution().App("App",
        globalUsings: SharedLibrary.Factories,
        files:
        [
            ("Model/Receipt.cs", $"using eQuantic.UI.Primitives;\nnamespace App.Model;\n\n{Type(kind, member)}\n"),
            ("Screens/Home.cs",
                $$"""
                using eQuantic.UI.Primitives;
                using App.Model;
                namespace App.Screens;

                public sealed class Home : StatelessComponent
                {
                    public override VisualNode Build(ComponentContext context) =>
                        Text($"{new Receipt({{(kind == "a record" ? "7" : "")}})}", TypeRole.BodyM, context.Theme.TextPrimary, maxLines: 1);
                }
                """),
        ]);

    [Theory]
    [InlineData("a class", "ToString")]
    [InlineData("a class", "Equals")]
    [InlineData("a class", "GetHashCode")]
    [InlineData("a record", "ToString")]
    [InlineData("a record", "Equals")]
    [InlineData("a record", "GetHashCode")]
    [InlineData("a struct", "ToString")]
    public void AServerOnlyOverride_OfWhatTheRuntimeCalls_FailsTheBuild_WhereItIsDeclared(string kind, string name)
    {
        // A record's Equals(object) cannot be declared in C#: its typed Equals is the one it may write.
        var member = (kind, name) is ("a record", "Equals")
            ? "[ServerOnly] public bool Equals(Receipt? other) => System.IO.File.Exists(\"r.txt\");"
            : Member(name);
        using var solution = App(kind, member);
        var built = solution.Build();

        if ((kind, name) is ("a record", "Equals"))
        {
            built.Failed.Should().BeFalse("a typed Equals is a member of its own, which a call fence answers for");
            return;
        }
        built.Failed.Should().BeTrue($"{kind}'s {name} is what the browser runs for it");
        built.ErrorsIn("App").Should().ContainSingle()
            .Which.Should().StartWith(solution.FileOf("App", "Model/Receipt.cs") + "(")
            .And.Contain("error EQ2010").And.Contain($"'Receipt.{name}'");
    }

    [Fact]
    public void AnOverloadOfThoseNames_StaysOnTheServerFreely()
    {
        using var solution = App("a class", "[ServerOnly] public string ToString(string format) => System.IO.File.ReadAllText(format);");
        var built = solution.Build();

        built.Failed.Should().BeFalse($"nothing but the code calls an overload:\n{built.Errors}");
    }

    [Fact]
    public void ATypeThatStaysOnTheServer_KeepsItsOverride()
    {
        using var solution = new Solution().App("App", globalUsings: SharedLibrary.Factories, files:
        [
            ("Model/Vault.cs",
                """
                using eQuantic.UI.Primitives;
                namespace App.Model;

                [ServerOnly]
                public sealed class Vault
                {
                    [ServerOnly] public override string ToString() => System.IO.File.ReadAllText("r.txt");
                }
                """),
            ("Screens/Home.cs",
                """
                using eQuantic.UI.Primitives;
                namespace App.Screens;

                public sealed class Home : StatelessComponent
                {
                    public override VisualNode Build(ComponentContext context) =>
                        Text("plain", TypeRole.BodyM, context.Theme.TextPrimary, maxLines: 1);
                }
                """),
        ]);
        var built = solution.Build();

        built.Failed.Should().BeFalse($"a type that never crosses has no twin to miss the member:\n{built.Errors}");
    }
}
