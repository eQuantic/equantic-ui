namespace eQuantic.UI.Compiler.Tests.ReferencedProjects;

/// <summary>
/// The solution #624 is about, as source: an app (<c>App</c>), the class library it references
/// (<c>Common</c>) and the project that library references and the app does not (<c>Domain</c>). It
/// holds one of each shape a shared library has: a static class read by name, a record a page builds,
/// a component a page places, a record that holds a record of the second project, a catalogue of its
/// own, and server code no page names. <c>samples/SharedLibrary</c> is the same solution as projects,
/// built by CI and proven in a browser. This one is what the tests bend.
/// </summary>
internal static class SharedLibrary
{
    /// <summary>The factory surface in scope in every file, as the SDK puts it in an app's and a
    /// library's project file puts it in its own.</summary>
    public const string Factories = "global using static eQuantic.UI.Components.UI;\n";

    public const string Money =
        """
        namespace Domain;

        public sealed record Money(decimal Amount, string Currency)
        {
            public Money Plus(Money other) => this with { Amount = Amount + other.Amount };
        }
        """;

    /// <summary>Server code beside the types a page uses: it reads a file.</summary>
    public const string ReceiptArchive =
        """
        namespace Domain;

        public sealed class ReceiptArchive(string root)
        {
            public bool Holds(string name) => File.Exists(Path.Combine(root, name));
        }
        """;

    public const string Palette =
        """
        using eQuantic.UI.Primitives;

        namespace Common;

        public static class Palette
        {
            public static readonly ColorToken Cobalt = new(Color.FromRgba(0x23, 0x47, 0xA8, 0xFF));
            public static readonly string[] Names = ["Cobalt", "Terracotta"];
        }
        """;

    public const string Cursor =
        """
        namespace Common;

        public sealed record Cursor(int Position)
        {
            public Cursor Next() => new(Position + 1);
        }
        """;

    public const string Receipt =
        """
        using Domain;

        namespace Common;

        public sealed record Receipt(string Title, Money Total, long Number, DateTimeOffset IssuedAt, Guid Id);
        """;

    public const string Wordmark =
        """
        using eQuantic.UI.Primitives;
        using Common.Resources;

        namespace Common;

        public sealed class Wordmark : StatelessComponent
        {
            public override VisualNode Build(ComponentContext context) =>
                Row(gap: Space.S2, children:
                [
                    Box(new BoxStyle { Background = Palette.Cobalt }),
                    Text(Strings.Brand, TypeRole.Title, context.Theme.TextPrimary, maxLines: 1),
                    Text(Strings.Title, TypeRole.Caption, context.Theme.TextMuted, maxLines: 1),
                ]);
        }
        """;

    /// <summary>A page that uses everything <c>Common</c> holds for the browser, and names its server
    /// code only inside a Server Action, whose body never crosses.</summary>
    public const string Home =
        """
        using eQuantic.UI.Components;
        using eQuantic.UI.Primitives;
        using App.Resources;
        using Common;
        using Domain;

        namespace App.Screens;

        public sealed class Home : StatefulComponent
        {
            private Cursor _cursor = new(0);
            private Receipt? _receipt;

            [ServerAction]
            public Task<Receipt> LoadReceipt()
            {
                var archived = new ReceiptArchive(AppContext.BaseDirectory).Holds("receipt-1.txt");
                return Task.FromResult(new Receipt(archived ? "Archived" : "Order", new Money(12.5m, "EUR"),
                    9_007_199_254_740_993, DateTimeOffset.UnixEpoch, Guid.Empty));
            }

            private async Task Load()
            {
                var receipt = await LoadReceipt();
                SetState(() => _receipt = receipt);
            }

            public override VisualNode Build(ComponentContext context) =>
                Column(gap: Space.S4, children: [
                    new Wordmark(),
                    Text(Strings.Title, TypeRole.Heading, context.Theme.TextPrimary, maxLines: 1),
                    Text(string.Join(", ", Palette.Names), TypeRole.BodyM, Palette.Cobalt, maxLines: 1),
                    Button("Next", onPressed: () => SetState(() => _cursor = _cursor.Next())),
                    Text(_cursor == new Cursor(2) ? "two" : "not two", TypeRole.Caption, context.Theme.TextMuted, maxLines: 1),
                    Button("Load", onPressed: () => _ = Load()),
                    Text(_receipt is null ? "none" : _receipt.Title, TypeRole.BodyM, context.Theme.TextPrimary, maxLines: 1),
                ]);
        }
        """;

    /// <summary>A resource class as the IDE writes it beside a <c>.resx</c>, with one accessor a key.</summary>
    public static string Designer(string @namespace, params string[] keys) =>
        $$"""
        namespace {{@namespace}}
        {
            internal class Strings
            {
                private static global::System.Resources.ResourceManager? resourceMan;

                internal Strings() { }

                internal static global::System.Resources.ResourceManager ResourceManager =>
                    resourceMan ??= new global::System.Resources.ResourceManager("{{@namespace}}.Strings", typeof(Strings).Assembly);

                internal static global::System.Globalization.CultureInfo? Culture { get; set; }

        {{string.Concat(keys.Select(key => $"        internal static string {key} => ResourceManager.GetString(\"{key}\", Culture)!;\n"))}}    }
        }
        """;

    /// <summary>A <c>.resx</c> with these values.</summary>
    public static string Resx(params (string Key, string Value)[] values) =>
        "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n<root>\n"
        + string.Concat(values.Select(pair =>
            $"  <data name=\"{pair.Key}\" xml:space=\"preserve\">\n    <value>{pair.Value}</value>\n  </data>\n"))
        + "</root>\n";

    /// <summary>The whole solution, ready to build. A test adds to it or writes over a file of it.</summary>
    public static Solution Solution() =>
        new Solution()
            .Library("Domain", files:
            [
                ("Money.cs", Money),
                ("ReceiptArchive.cs", ReceiptArchive),
            ])
            .Library("Common", references: ["Domain"], globalUsings: Factories, files:
            [
                ("Palette.cs", Palette),
                ("Cursor.cs", Cursor),
                ("Receipt.cs", Receipt),
                ("Wordmark.cs", Wordmark),
                ("Resources/Strings.Designer.cs", Designer("Common.Resources", "Brand", "Title", "Unused")),
                ("Resources/Strings.resx", Resx(("Brand", "Shared"), ("Title", "A mark"), ("Unused", "Nobody reads this"))),
                ("Resources/Strings.pt-BR.resx", Resx(("Brand", "Compartilhada"), ("Title", "Uma marca do Brasil"), ("Unused", "x"))),
                ("Resources/Strings.pt-PT.resx", Resx(("Brand", "Partilhada"), ("Title", "Uma marca de Portugal"), ("Unused", "x"))),
            ])
            .App("App", references: ["Common"], globalUsings: Factories, files:
            [
                ("Screens/Home.cs", Home),
                ("Resources/Strings.Designer.cs", Designer("App.Resources", "Title")),
                ("Resources/Strings.resx", Resx(("Title", "One page"))),
                ("Resources/Strings.pt-BR.resx", Resx(("Title", "Uma página do Brasil"))),
                ("Resources/Strings.pt-PT.resx", Resx(("Title", "Uma página de Portugal"))),
            ]);
}
