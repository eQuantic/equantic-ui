using eQuantic.UI.Compiler.Tests.ReferencedProjects;
using FluentAssertions;

namespace eQuantic.UI.Compiler.Tests;

/// <summary>
/// A server mark on an accessor is refused where it is declared (#1512). C# takes <c>[ServerAction]</c> and
/// <c>[ServerOnly]</c>, a method's attributes, on any accessor. A <c>[ServerAction]</c> accessor is no action: no
/// stub calls it, the member it belongs to cannot carry the mark, and an event's handler or a getter's answer
/// cannot cross the wire. A <c>[ServerOnly]</c> property or indexer accessor was written into its twin, its
/// server code refused as client code and anything else shipped. Both built without a word.
/// </summary>
public sealed class AServerMarkOnAnAccessorTests
{
    private const string Home = """
        using System;
        using eQuantic.UI.Primitives;
        using App.Model;
        namespace App.Screens;

        public sealed class Home : StatelessComponent
        {
            {{members}}

            public override VisualNode Build(ComponentContext context) =>
                Text("home", TypeRole.BodyM, context.Theme.TextPrimary, maxLines: 1);
        }
        """;

    private const string NoModel = """
        namespace App.Model;
        public sealed class Unused { }
        """;

    public static TheoryData<string, string, string, string> Marks => new()
    {
        {
            "a component's event add marked [ServerAction]", "Screens/Home.cs",
            "private Action? _f; public event Action Fired { [ServerAction] add => _f += value; remove => _f -= value; }",
            "Fired"
        },
        // The two #1445 rows that read a component's [ServerAction] accessor as kept on the server: its remove
        // beside an add that crosses, and its add subscribed from client code, refused there with EQ2010.
        {
            "a component's event remove marked [ServerAction]", "Screens/Home.cs",
            "private Action? _served; public event Action Served { add => _served += value; [ServerAction] remove { _ = System.IO.File.Exists(\"served\"); _served -= value; } }",
            "Served"
        },
        {
            "a component's event add marked [ServerAction], subscribed from client code", "Screens/Home.cs",
            "private Action? _f; public event Action Moved { [ServerAction] add => _f += value; remove => _f -= value; } public static void Take(Home m, Action a) => m.Moved += a;",
            "Moved"
        },
        {
            "a component's property get marked [ServerAction]", "Screens/Home.cs",
            "public string Label { [ServerAction] get => \"x\"; }",
            "Label"
        },
        {
            "a record's property get marked [ServerOnly]", "Model/Card.cs",
            "public static string Read(Card c) => c.Note;",
            "Note"
        },
        {
            "a class's indexer get marked [ServerOnly]", "Model/Ledger.cs",
            "public static int At(Ledger l) => l[0];",
            "Ledger"
        },
        {
            "a class's event add marked [ServerAction]", "Model/Vault.cs",
            "public static void Take(Vault v) => v.Opened += () => { };",
            "Opened"
        },
    };

    private static readonly Dictionary<string, string> Models = new()
    {
        ["Model/Card.cs"] = """
            using eQuantic.UI.Primitives;
            namespace App.Model;
            public sealed record Card(int Id) { public string Note { [ServerOnly] get => "kept on the server"; } }
            """,
        ["Model/Ledger.cs"] = """
            using eQuantic.UI.Primitives;
            namespace App.Model;
            public sealed class Ledger { public int this[int i] { [ServerOnly] get => i + 42; } }
            """,
        ["Model/Vault.cs"] = """
            using System;
            using eQuantic.UI.Primitives;
            namespace App.Model;
            public sealed class Vault { private Action? _f; public event Action Opened { [ServerAction] add => _f += value; remove => _f -= value; } }
            """,
    };

    [Theory]
    [MemberData(nameof(Marks))]
    public void AServerMarkOnAnAccessor_FailsTheBuild_WhereItIsDeclared(string name, string declaredIn, string members, string member)
    {
        var model = Models.TryGetValue(declaredIn, out var text) ? (declaredIn, text) : ("Model/Unused.cs", NoModel);
        using var solution = new Solution().App("App", globalUsings: SharedLibrary.Factories,
            files: [model, ("Screens/Home.cs", Home.Replace("{{members}}", members))]);

        var built = solution.Build();

        built.Failed.Should().BeTrue($"{name}: the mark makes no action and keeps nothing on the server");
        built.ErrorsIn("App").Should().Contain(line => line.StartsWith(solution.FileOf("App", declaredIn) + "(")
                && line.Contains("error EQ2016") && line.Contains(member),
            $"{name}: the refusal is told at the accessor, naming its member:\n{built.Errors}");
    }
}
