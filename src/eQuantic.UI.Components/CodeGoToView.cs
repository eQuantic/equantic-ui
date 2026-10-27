using eQuantic.UI.Code;
using eQuantic.UI.Primitives;

namespace eQuantic.UI.Components;

/// <summary>
/// The go-to field an editor draws over the top of its code while the person goes to a line or a symbol
/// (<see cref="CodeGoTo"/>, #1083), or picks one of the places a name is defined or used (#1084), as VS Code's
/// quick input stands over its editor. The field is a <see cref="TextEntry"/> holding the session's text, prefix
/// and all, and under it either a page of the symbols or the places it lists, the selected one marked, or what it
/// says about the line it reads.
/// <para>
/// The list is the field's own listbox (<see cref="Anchored"/> with <see cref="AnchorPanelRole.Listbox"/>, as
/// a <see cref="Select"/>'s is): the field's text control is the combobox, its rows are options, and the
/// selected one is the field's active descendant, so a screen reader follows the arrows while the field keeps
/// the keyboard. What
/// it says about a line is a polite live region, read as it changes. A row looks as the completion's do: the
/// kind's letter in its colour, and the characters the text matched in the accent colour. A deprecated
/// symbol is drawn in the muted ink, where VS Code strikes it through: the vocabulary has no strike for text
/// yet (#1089).
/// </para>
/// </summary>
internal static class CodeGoToView
{
    /// <summary>The widest the field grows, as VS Code's quick input.</summary>
    internal const float MaxWidth = 560;

    /// <summary>The rows a page of the list holds.</summary>
    internal const int PageRows = 10;

    /// <summary>
    /// The field, <paramref name="width"/> wide, and under it the page of symbols from <paramref name="top"/>
    /// or what it says. What is typed goes to <paramref name="typed"/>, Enter to <paramref name="accept"/>, a
    /// field losing the keyboard to <paramref name="dismiss"/> and a press on a row to <paramref name="pick"/>.
    /// The list hangs with no scrim: a press in the code lands where the person pointed, and the field closes
    /// as it loses the keyboard, as VS Code's quick input hides.
    /// </summary>
    internal static VisualNode Build(ComponentContext context, CodeEditorController editor, CodeBlock.CodeMetrics metrics,
        float width, int top, Action<string> typed, Action accept, Action dismiss, Action<int> pick)
    {
        var goTo = editor.GoTo;
        var theme = context.Theme;
        var places = Lists(goTo.Mode);
        var listed = (goTo.Mode == CodeGoToMode.Symbol || places) && goTo.Entries.Count > 0;
        var field = new TextEntry(goTo.Query, typed)
        {
            // A list of places is named by what it lists, and its text only filters it.
            Label = places ? Title(goTo, goTo.Places.Count) : SdkStrings.GoTo,
            Placeholder = places ? SdkStrings.FilterPlaces : SdkStrings.GoToStart,
            Autofocus = true,
            OnSubmit = accept,
            OnFocusChanged = focused =>
            {
                if (!focused) dismiss();
            },
        };
        var panel = listed
            ? List(theme, metrics, goTo, width, top, pick)
            : Said(theme, metrics, Saying(editor));
        // The field in the find bar's coat, so the code under it does not show through; the list hangs from
        // its edges.
        var coated = new Box(new BoxStyle
        {
            Width = SizeValue.Fill,
            Background = theme.Surface,
            BorderWidth = 1,
            BorderColor = theme.Border,
            CornerRadius = new CornerRadii(theme.Shape(ShapeScale.Medium)),
            Padding = EdgeInsets.Symmetric(Space.S2, Space.S1),
            Shadow = theme.Elevation(2),
        }, field);
        return new Box(new BoxStyle { Width = width }, new Anchored(coated, panel)
        {
            Open = true,
            PanelRole = listed ? AnchorPanelRole.Listbox : AnchorPanelRole.None,
            ActiveIndex = listed ? goTo.Selected - top : -1,
            MatchAnchorWidth = true,
        });
    }

    /// <summary>
    /// What the field says when it lists nothing: how to start, what a line it reads does, or why there is no
    /// symbol to list, in VS Code's words.
    /// </summary>
    internal static string Saying(CodeEditorController editor)
    {
        var goTo = editor.GoTo;
        if (goTo.Mode == CodeGoToMode.Line)
        {
            if (goTo.Line is not { } line) return SdkStrings.GoToLinePrompt(editor.Document.LineCount);
            if (line.Column is { } column) return SdkStrings.GoToPosition(line.Line, column);
            return line.ColumnStarted ? SdkStrings.GoToColumnPrompt(line.Line, line.Columns) : SdkStrings.GoToLineOrColumn(line.Line);
        }
        if (goTo.Mode == CodeGoToMode.Symbol)
        {
            if (editor.Symbols.Providers.Count == 0) return SdkStrings.NoSymbolProvider;
            if (goTo.IsAsking) return SdkStrings.ReadingSymbols;
            return goTo.Query.Substring(1).Trim().Length == 0 ? SdkStrings.NoSymbols : SdkStrings.NoMatchingSymbols;
        }
        // A list of places opens only with places in it, so what it says is that the text matches none.
        if (Lists(goTo.Mode)) return SdkStrings.NoMatchingPlaces;
        return SdkStrings.GoToStart;
    }

    /// <summary>Whether <paramref name="mode"/> lists places (#1084).</summary>
    internal static bool Lists(CodeGoToMode mode) => mode == CodeGoToMode.Definitions || mode == CodeGoToMode.References;

    /// <summary>The heading over a list of places, <paramref name="count"/> of them: what they are, in VS Code's
    /// words for the list.</summary>
    internal static string Title(CodeGoTo goTo, int count) => goTo.Mode == CodeGoToMode.References
        ? SdkStrings.ReferencesCount(count)
        : goTo.DefinitionKind switch
        {
            CodeDefinitionKind.Declaration => SdkStrings.DeclarationsCount(count),
            CodeDefinitionKind.TypeDefinition => SdkStrings.TypeDefinitionsCount(count),
            CodeDefinitionKind.Implementation => SdkStrings.ImplementationsCount(count),
            _ => SdkStrings.DefinitionsCount(count),
        };

    /// <summary>What the editor says when nothing was found where the person asked, in VS Code's words: for the
    /// word there, or with none, for nothing in particular.</summary>
    internal static string NotFoundText(CodeGoToNotFound nothing)
    {
        var word = nothing.Word;
        var named = word.Length > 0;
        if (nothing.Mode == CodeGoToMode.References) return named ? SdkStrings.NoReferencesFound(word) : SdkStrings.NoReferences;
        return nothing.Kind switch
        {
            CodeDefinitionKind.Declaration => named ? SdkStrings.NoDeclarationFound(word) : SdkStrings.NoDeclaration,
            CodeDefinitionKind.TypeDefinition => named ? SdkStrings.NoTypeDefinitionFound(word) : SdkStrings.NoTypeDefinition,
            CodeDefinitionKind.Implementation => named ? SdkStrings.NoImplementationFound(word) : SdkStrings.NoImplementation,
            _ => named ? SdkStrings.NoDefinitionFound(word) : SdkStrings.NoDefinition,
        };
    }

    private static VisualNode Said(IAppTheme theme, CodeBlock.CodeMetrics metrics, string saying) =>
        new LiveRegion(Panel(theme, new Box(new BoxStyle
        {
            Width = SizeValue.Fill,
            Padding = EdgeInsets.Symmetric(Space.S2, Space.S1),
        }, new Text(saying, TypeRole.LabelSmall, theme.TextMuted) { StyleOverride = metrics.Style with { Mono = false } })));

    private static VisualNode List(IAppTheme theme, CodeBlock.CodeMetrics metrics, CodeGoTo goTo, float width, int top,
        Action<int> pick)
    {
        var entries = goTo.Entries;
        // The columns a row has for its name and its container: the field's width less a row's chrome.
        var columns = (int)MathF.Floor((width - 2 * Space.S2 - metrics.LineHeight - Space.S1) / metrics.ColumnWidth);
        var page = new Column(gap: 0) { Width = SizeValue.Fill };
        var heading = Lists(goTo.Mode) ? Title(goTo, entries.Count) : SdkStrings.SymbolsCount(entries.Count);
        page.Add(new Box(new BoxStyle { Width = SizeValue.Fill, Padding = EdgeInsets.Symmetric(Space.S2, Space.S1) },
            new Text(heading, TypeRole.LabelSmall, theme.TextMuted, maxLines: 1)));
        for (var i = top; i < top + PageRows && i < entries.Count; i++)
        {
            var index = i;
            page.Add(Entry(theme, metrics, entries[i], i == goTo.Selected, columns, () => pick(index)));
        }
        return Panel(theme, page);
    }

    private static VisualNode Panel(IAppTheme theme, VisualNode content) => new Box(new BoxStyle
    {
        Width = SizeValue.Fill,
        Background = theme.Surface,
        CornerRadius = new CornerRadii(theme.Shape(ShapeScale.Medium)),
        BorderWidth = 1,
        BorderColor = theme.Border,
        Elevation = 2,
        Padding = EdgeInsets.Symmetric(0, Space.S1),
        Clip = true,
    }, content);

    /// <summary>
    /// One symbol or place, as an option the keyboard never lands on: a press goes to it while the field keeps the
    /// keyboard. Its name is its own and its container's, so the letter before them is not read out. The name
    /// and the container fit the row's columns as the completion's label and detail do, the container cut
    /// first. A place has no kind, and no letter before it: its name is the text of its line.
    /// </summary>
    private static VisualNode Entry(IAppTheme theme, CodeBlock.CodeMetrics metrics, CodeGoToEntry entry, bool selected,
        int columns, Action pressed)
    {
        var ink = entry.Deprecated ? theme.TextMuted : selected ? theme.Colors(Variant.Primary).OnSubtle : theme.TextPrimary;
        var accent = theme.Colors(Variant.Primary).Base;
        var name = CodeCompletionView.Fit(entry.Name, columns);
        var row = new Row(gap: Space.S1) { Width = SizeValue.Fill, Height = SizeValue.Fill };
        if (entry.Kind is { } kind) row.Add(CodeCompletionView.Glyph(theme, metrics, KindOf(kind)));
        row.Add(new Text(name, TypeRole.LabelSmall, ink, maxLines: 1)
        {
            Mono = true,
            StyleOverride = metrics.Style,
            Spans = CodeCompletionView.Marked(name, entry.Matches, ink, accent),
        });
        // The container has what the name left, less the two columns between them.
        var room = columns - CodeLineCells.WidthOf(name, 4) - 2;
        if (entry.Container is { Length: > 0 } held && room >= 2)
        {
            var container = CodeCompletionView.Fit(held, room);
            row.Add(new Text(container, TypeRole.LabelSmall, theme.TextMuted, maxLines: 1)
            {
                Mono = true,
                StyleOverride = metrics.Style,
                Spans = CodeCompletionView.Marked(container, entry.ContainerMatches, theme.TextMuted, accent),
            });
        }

        return new Pressable(new Box(new BoxStyle
        {
            Width = SizeValue.Fill,
            Height = metrics.LineHeight,
            Padding = EdgeInsets.Symmetric(Space.S2, 0),
            Background = selected ? theme.Colors(Variant.Primary).Subtle : null,
            Hover = selected ? null : new StyleDiff { Background = theme.SurfaceSubtle },
        }, row), pressed)
        {
            Role = PressableRole.Option,
            Selected = selected,
            CanRequestFocus = false,
            Label = entry.Container is { Length: > 0 } container2 ? entry.Name + ", " + container2 : entry.Name,
        };
    }

    /// <summary>The completion's kind that draws a symbol's: the same letter and colour for what the two lists
    /// both name, and a value's for the kinds a completion has no word for.</summary>
    internal static CodeCompletionKind KindOf(CodeSymbolKind kind) => kind switch
    {
        CodeSymbolKind.File => CodeCompletionKind.File,
        CodeSymbolKind.Module or CodeSymbolKind.Namespace or CodeSymbolKind.Package => CodeCompletionKind.Module,
        CodeSymbolKind.Class => CodeCompletionKind.Class,
        CodeSymbolKind.Method => CodeCompletionKind.Method,
        CodeSymbolKind.Property => CodeCompletionKind.Property,
        CodeSymbolKind.Field => CodeCompletionKind.Field,
        CodeSymbolKind.Constructor => CodeCompletionKind.Constructor,
        CodeSymbolKind.Enum => CodeCompletionKind.Enum,
        CodeSymbolKind.Interface => CodeCompletionKind.Interface,
        CodeSymbolKind.Function => CodeCompletionKind.Function,
        CodeSymbolKind.Variable => CodeCompletionKind.Variable,
        CodeSymbolKind.Constant => CodeCompletionKind.Constant,
        CodeSymbolKind.EnumMember => CodeCompletionKind.EnumMember,
        CodeSymbolKind.Struct => CodeCompletionKind.Struct,
        CodeSymbolKind.Event => CodeCompletionKind.Event,
        CodeSymbolKind.Operator => CodeCompletionKind.Operator,
        CodeSymbolKind.TypeParameter => CodeCompletionKind.TypeParameter,
        _ => CodeCompletionKind.Value,
    };
}
