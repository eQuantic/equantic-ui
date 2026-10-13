using eQuantic.UI.Components;
using eQuantic.UI.Primitives;
using SharedLibrary.Common;
using SharedLibrary.Domain;
using SharedLibrary.Web.Resources;

namespace SharedLibrary.Web.Pages;

/// <summary>
/// One page that uses a class library the way an app that outgrew one project does. Everything it
/// takes from <c>SharedLibrary.Common</c> works in the browser as it does on the server: the
/// component in its tree, the static tokens it reads, the record it builds on a click, the record a
/// Server Action answers, the strings of the library's own catalogue, an internal record the
/// library opens to this app by its assembly's name, and a value the library reads from a project it
/// references through a constant alone.
/// </summary>
[Page("/", Title = "Shared library")]
public sealed class HomePage : StatefulComponent
{
    /// <summary>A record of the library, built in the browser on every press.</summary>
    private Cursor _cursor = new(0);

    /// <summary>What the Server Action answered, or null before it is asked.</summary>
    private Receipt? _receipt;

    private static readonly CultureOption[] Languages =
    [
        new("en", "English"),
        new("pt-BR", "Português (Brasil)"),
        new("pt-PT", "Português (Portugal)"),
    ];

    /// <summary>
    /// Runs on the server, and its body never crosses: it may name the library's server code as
    /// freely as it names the record it answers. The number is past 2^53, the instant carries an
    /// offset and the id is a Guid, so each has to be read in the browser as what it is.
    /// </summary>
    [ServerAction]
    public Task<Receipt> LoadReceipt()
    {
        var archived = new ReceiptArchive(AppContext.BaseDirectory).Holds("receipt-1.txt");
        return Task.FromResult(new Receipt(
            archived ? "Archived order" : "Order",
            new Money(12.50m, "EUR").Plus(new Money(0.25m, "EUR")),
            9_007_199_254_740_993,
            new DateTimeOffset(2026, 10, 12, 9, 30, 0, TimeSpan.FromHours(1)),
            Guid.Parse("3f2504e0-4f89-11d3-9a0c-0305e82c3301")));
    }

    private async Task Load()
    {
        var receipt = await LoadReceipt();
        SetState(() => _receipt = receipt);
    }

    public override VisualNode Build(ComponentContext context)
    {
        var theme = context.Theme;

        return Box(new BoxStyle
        {
            Width = SizeValue.Fill,
            Height = SizeValue.Fill,
            Background = theme.Background,
            Padding = EdgeInsets.All(Space.S6),
        },
        Column(gap: Space.S4, children: [
            // The library's component. Its name comes from the LIBRARY's catalogue.
            new Wordmark(),
            // The app's own catalogue has a Title too, and so does the library's: two catalogues of
            // one class name, each read showing its own project's text.
            Text(Strings.Title, TypeRole.Heading, theme.TextPrimary, maxLines: 2),

            CultureSwitcher(Languages),

            // A static member of the library, read by name.
            Row(gap: Space.S2, children: [
                Text(string.Join(", ", Palette.Names), TypeRole.BodyM, Palette.Terracotta, maxLines: 1),
            ]),

            // A record of the library, built in the browser: equal to another at the same position.
            Row(gap: Space.S3, cross: CrossAlign.Center, children: [
                Button("Next", onPressed: () => SetState(() => _cursor = _cursor.Next())),
                Text($"Position {_cursor.Position}", TypeRole.BodyM, theme.TextPrimary, maxLines: 1),
                Text(_cursor == new Cursor(2) ? "equal to Cursor(2)" : "not Cursor(2)",
                    TypeRole.Caption, theme.TextMuted, maxLines: 1),
            ]),

            // A record of the library answered by the server, with a record of the Domain inside.
            Row(gap: Space.S3, cross: CrossAlign.Center, children: [
                Button("Load receipt", Variant.Outline, onPressed: () => _ = Load()),
                Text(Describe(_receipt), TypeRole.BodyM, theme.TextSecondary, maxLines: 2),
            ]),

            // An INTERNAL record of the library, which it opens to this app by its assembly's name.
            Text(new Imprint("sample").Line, TypeRole.Caption, theme.TextMuted, maxLines: 1),

            // A value the library reads from the Theme project through a constant, which leaves no
            // trace of that project in the library's assembly (#887).
            Text($"Headings in {Typeface.Family}", TypeRole.Caption, theme.TextMuted, maxLines: 1),
        ]));
    }

    /// <summary>The receipt in words that only come out right when each member has its own type:
    /// the next number, the year of the instant and its offset, and the id compared as a Guid.</summary>
    private static string Describe(Receipt? receipt) =>
        receipt is null
            ? "No receipt yet"
            : $"{receipt.Title}: {receipt.Total.Display}, next number {receipt.Number + 1}, "
              + $"issued {receipt.IssuedAt.Year} at +{receipt.IssuedAt.Offset.Hours}h, "
              + (receipt.Id == Guid.Parse("3f2504e0-4f89-11d3-9a0c-0305e82c3301") ? "known id" : "unknown id");
}
