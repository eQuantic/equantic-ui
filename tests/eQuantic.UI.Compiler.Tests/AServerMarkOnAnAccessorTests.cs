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
        // Gate round 1 of #1512: an interface's default member, whose accessors reach every implementing twin.
        {
            "an interface's default event add marked [ServerAction]", "Model/ITicker.cs",
            "public static void Use(Clock c) { }",
            "Ticked"
        },
        {
            "an interface's default property get marked [ServerOnly]", "Model/ISecret.cs",
            "public static string Read(Safe s) => ((ISecret)s).Secret;",
            "Secret"
        },
        // A nested type is a module of its own (#584), checked as one.
        {
            "a nested class's property get marked [ServerAction]", "Screens/Home.cs",
            "public sealed class Badge { public string Label { [ServerAction] get => \"x\"; } } public static string Read(Badge b) => b.Label;",
            "Label"
        },
        // A partial type's second declaration, in a file of its own, is a module of its own too.
        {
            "a partial class's other declaration's get marked [ServerOnly]", "Model/Gadget.Part.cs",
            "public static int Read(Gadget g) => g.A + g.B;",
            "B"
        },
        // A field-like event's accessors take [method: …], and the compiler writes them.
        {
            "a field-like event's accessors marked [method: ServerAction]", "Model/Bell.cs",
            "public static void Take(Bell b) => b.Rang += () => { };",
            "Rang"
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
        ["Model/ITicker.cs"] = """
            using System;
            using eQuantic.UI.Primitives;
            namespace App.Model;
            public interface ITicker { event Action Ticked { [ServerAction] add { _ = System.IO.File.Exists("ticked"); } remove { } } }
            public sealed class Clock : ITicker { }
            """,
        ["Model/ISecret.cs"] = """
            using eQuantic.UI.Primitives;
            namespace App.Model;
            public interface ISecret { string Secret { [ServerOnly] get => "kept on the server"; } }
            public sealed class Safe : ISecret { }
            """,
        ["Model/Gadget.Part.cs"] = """
            using eQuantic.UI.Primitives;
            namespace App.Model;
            public sealed partial class Gadget { public int B { [ServerOnly] get => 2; } }
            """,
        ["Model/Bell.cs"] = """
            using System;
            using eQuantic.UI.Primitives;
            namespace App.Model;
            public sealed class Bell { [method: ServerAction] public event Action? Rang; }
            """,
    };

    /// <summary>The other files a row's type is declared across.</summary>
    private static readonly Dictionary<string, (string Path, string Text)> Companions = new()
    {
        ["Model/Gadget.Part.cs"] = ("Model/Gadget.cs", """
            namespace App.Model;
            public sealed partial class Gadget { public int A => 1; }
            """),
    };

    [Theory]
    [MemberData(nameof(Marks))]
    public void AServerMarkOnAnAccessor_FailsTheBuild_WhereItIsDeclared(string name, string declaredIn, string members, string member)
    {
        var model = Models.TryGetValue(declaredIn, out var text) ? (declaredIn, text) : ("Model/Unused.cs", NoModel);
        (string, string)[] files = [model, ("Screens/Home.cs", Home.Replace("{{members}}", members))];
        if (Companions.TryGetValue(declaredIn, out var companion)) files = [.. files, companion];
        using var solution = new Solution().App("App", globalUsings: SharedLibrary.Factories, files: files);

        var built = solution.Build();

        built.Failed.Should().BeTrue($"{name}: the mark makes no action and keeps nothing on the server");
        built.ErrorsIn("App").Should().Contain(line => line.StartsWith(solution.FileOf("App", declaredIn) + "(")
                && line.Contains("error EQ2016") && line.Contains(member),
            $"{name}: the refusal is told at the accessor, naming its member:\n{built.Errors}");
    }
    /// <summary>
    /// <c>[ServerOnly]</c> on an event's accessor is what keeps it on the server (#1445), and is no EQ2016: the
    /// build succeeds, the subscription through the accessor that crosses runs, and no twin holds the server-only
    /// body. A refusal widened to every <c>[ServerOnly]</c> accessor broke this, and only a conformance row,
    /// which skips without a bun, said so.
    /// </summary>
    [Fact]
    public void AServerOnlyEventAccessor_Builds_AndStaysOutOfTheTwin()
    {
        const string vault = """
            using System;
            using eQuantic.UI.Primitives;
            namespace App.Model;
            public sealed class Vault
            {
                private Action? _f;
                public event Action Opened { add => _f += value; [ServerOnly] remove { _ = System.IO.File.Exists("vault"); _f -= value; } }
            }
            """;
        using var solution = new Solution().App("App", globalUsings: SharedLibrary.Factories,
            files: [("Model/Vault.cs", vault), ("Screens/Home.cs", Home.Replace("{{members}}", "public static void Take(Vault v) => v.Opened += () => { };"))]);

        var built = solution.Build();

        built.Failed.Should().BeFalse($"a [ServerOnly] event accessor stays on the server and is refused nowhere:\n{built.Errors}");
        built.ErrorsIn("App").Should().NotContain(line => line.Contains("EQ2016"));
        built.Module("Vault").Should().Contain("$add$opened").And.NotContain("$remove$opened").And.NotContain("\"vault\"");
    }
}
