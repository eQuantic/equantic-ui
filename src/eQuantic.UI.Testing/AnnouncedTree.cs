using System.Text.RegularExpressions;
using eQuantic.UI.Primitives;
using eQuantic.UI.Web;

namespace eQuantic.UI.Testing;

/// <summary>
/// WHAT A SCREEN READER ANNOUNCES for a lowered frame, computed from the ARIA the web realizer
/// wrote, the way the platform's accessibility tree is computed from a page: for the markup this
/// realizer writes, not for any page. On the web the semantics are written inline, so they are read
/// back from there rather than from the vocabulary, which is the only way a test sees what a
/// browser's reader is given.
/// <para>
/// The ROLE is the <c>role</c> attribute's, or the element's own (a <c>button</c>, an <c>a</c> with
/// <c>href</c>, an <c>input</c> by its type, a <c>textarea</c>, an <c>img</c> with a non-empty
/// <c>alt</c>, <c>h1</c> to <c>h6</c>). The NAME is <c>aria-labelledby</c>'s, then
/// <c>aria-label</c>'s, then <c>alt</c>'s, then, for a role named from its content, the text it
/// holds. A LEAF's subtree is its name, and a container is read and then walked into. What is
/// HIDDEN is passed over: an <c>aria-hidden="true"</c> or <c>hidden</c> subtree, and an element
/// whose class the style sink says hides it at the tester's width (an adaptive arm that does not
/// show there, <c>display:none</c>, <c>visibility:hidden</c>).
/// </para>
/// </summary>
internal sealed class AnnouncedTree
{
    /// <summary>The ARIA roles the realizer writes that the vocabulary has a word for. One it does not
    /// (a <c>listbox</c>, a <c>row</c>) is no stop of its own, and what it holds is walked as if it were
    /// not there. A live region (<c>status</c>, <c>log</c>, <c>alert</c>) is the vocabulary's live
    /// <see cref="SemanticRole.Group"/>, the stop Photon hands the platform's reader to mark live
    /// (#339).</summary>
    private static readonly Dictionary<string, SemanticRole> AriaRoles = new(StringComparer.Ordinal)
    {
        ["button"] = SemanticRole.Button,
        ["link"] = SemanticRole.Link,
        ["textbox"] = SemanticRole.TextField,
        ["searchbox"] = SemanticRole.TextField,
        ["slider"] = SemanticRole.Slider,
        ["img"] = SemanticRole.Image,
        ["image"] = SemanticRole.Image,
        ["checkbox"] = SemanticRole.Checkbox,
        ["switch"] = SemanticRole.Switch,
        ["gridcell"] = SemanticRole.GridCell,
        ["progressbar"] = SemanticRole.ProgressIndicator,
        ["group"] = SemanticRole.Group,
        ["status"] = SemanticRole.Group,
        ["log"] = SemanticRole.Group,
        ["alert"] = SemanticRole.Group,
        ["radio"] = SemanticRole.Radio,
        ["tab"] = SemanticRole.Tab,
        ["menuitem"] = SemanticRole.MenuItem,
        ["menuitemcheckbox"] = SemanticRole.MenuItem,
        ["menuitemradio"] = SemanticRole.MenuItem,
        ["option"] = SemanticRole.Option,
        ["tablist"] = SemanticRole.TabBar,
        ["radiogroup"] = SemanticRole.RadioGroup,
        ["combobox"] = SemanticRole.ComboBox,
        ["dialog"] = SemanticRole.Dialog,
        ["alertdialog"] = SemanticRole.AlertDialog,
        ["table"] = SemanticRole.Table,
        ["grid"] = SemanticRole.Grid,
        ["cell"] = SemanticRole.Cell,
        ["columnheader"] = SemanticRole.ColumnHeader,
        ["rowheader"] = SemanticRole.RowHeader,
    };

    private static readonly Regex Whitespace = new(@"\s+", RegexOptions.Compiled);

    private readonly StyleSink _styles;
    private readonly float _width;
    private readonly Dictionary<string, HtmlNode> _byId = new(StringComparer.Ordinal);
    private readonly Dictionary<HtmlNode, HtmlNode> _parents = new(ReferenceEqualityComparer.Instance);
    private readonly List<Announcement> _announced = new();
    private readonly List<HtmlNode> _nodes = new();

    /// <summary>The elements an <c>aria-owns</c> moves, each under the element that names it.</summary>
    private readonly Dictionary<HtmlNode, HtmlNode> _owners = new(ReferenceEqualityComparer.Instance);
    private readonly HashSet<HtmlNode> _walked = new(ReferenceEqualityComparer.Instance);

    private AnnouncedTree(HtmlNode root, StyleSink styles, float widthDp)
    {
        _styles = styles;
        _width = widthDp;
        Index(root);
        IndexOwnership(root);
        Walk(root);
    }

    /// <summary>Reads <paramref name="root"/>, lowered with <paramref name="styles"/>, as a viewport
    /// <paramref name="widthDp"/> wide shows it.</summary>
    internal static AnnouncedTree Read(HtmlNode root, StyleSink styles, float widthDp) => new(root, styles, widthDp);

    /// <summary>Every stop, in reading order.</summary>
    internal IReadOnlyList<Announcement> Announcements => _announced;

    /// <summary>The markup element the stop at <paramref name="index"/> was read from.</summary>
    internal HtmlNode NodeAt(int index) => _nodes[index];

    /// <summary>The element <paramref name="node"/> sits in, or null at the root.</summary>
    internal HtmlNode? ParentOf(HtmlNode node) => _parents.GetValueOrDefault(node);

    private void Index(HtmlNode root)
    {
        var pending = new Stack<HtmlNode>();
        pending.Push(root);
        while (pending.Count > 0)
        {
            var node = pending.Pop();
            if (Attribute(node, "id") is { } id) _byId.TryAdd(id, node);
            foreach (var child in node.Children)
            {
                _parents[child] = node;
                pending.Push(child);
            }
        }
    }

    /// <summary>
    /// What <c>aria-owns</c> moves. The accessibility tree takes an owned element out of its place in the
    /// document and reads it under the element that owns it, after that element's own children and in the
    /// order the attribute lists them: a table whose rows are written in bands, pinned headers apart from
    /// the scrolled cells, is read row by row only through it. An element owned twice stays with the first
    /// owner, and one that would own an element around it is ignored, as ARIA requires.
    /// </summary>
    private void IndexOwnership(HtmlNode root)
    {
        foreach (var owner in _parents.Keys.Prepend(root))
        {
            if (Attribute(owner, "aria-owns") is not { } ids) continue;
            foreach (var id in ids.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                if (!_byId.TryGetValue(id, out var owned) || IsAround(owned, owner)) continue;
                _owners.TryAdd(owned, owner);
            }
        }
    }

    private bool IsAround(HtmlNode candidate, HtmlNode node)
    {
        for (HtmlNode? at = node; at is not null; at = _parents.GetValueOrDefault(at))
            if (ReferenceEquals(at, candidate)) return true;
        return false;
    }

    /// <summary>The children the accessibility tree gives <paramref name="node"/>: its own, save those
    /// another element owns, then the ones it owns, in the order it names them.</summary>
    private IEnumerable<HtmlNode> ReadingChildren(HtmlNode node)
    {
        foreach (var child in node.Children)
            if (!_owners.ContainsKey(child)) yield return child;
        if (Attribute(node, "aria-owns") is not { } ids) yield break;
        foreach (var id in ids.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            if (_byId.TryGetValue(id, out var owned) && _owners.TryGetValue(owned, out var owner) && ReferenceEquals(owner, node))
                yield return owned;
    }

    private void Walk(HtmlNode node)
    {
        if (!_walked.Add(node)) return;
        if (node.Tag == "#text")
        {
            // A run of text no element around it named: a stop of its own, as a Text is.
            if (Collapse(node.TextContent) is { Length: > 0 } text)
                Add(node, new Announcement { Role = SemanticRole.StaticText, Name = text });
            return;
        }

        if (Hidden(node)) return;

        if (RoleOf(node, out var headingLevel) is { } role)
        {
            Add(node, Announce(node, role, headingLevel));
            if (!IsContainer(role)) return;
        }
        else if (node.Children.Count > 0 && node.Children.All(child => child.Tag == "#text"))
        {
            // An element holding only text is ONE stop, as one Text lowers to one element: its runs
            // are pieces of one sentence, not separate things to land on.
            if (Collapse(TextOf(node)) is { Length: > 0 } text)
                Add(node, new Announcement { Role = SemanticRole.StaticText, Name = text });
            return;
        }

        foreach (var child in ReadingChildren(node)) Walk(child);
    }

    private void Add(HtmlNode node, Announcement announcement)
    {
        _announced.Add(announcement);
        _nodes.Add(node);
    }

    private Announcement Announce(HtmlNode node, SemanticRole role, int headingLevel) => new()
    {
        Role = role,
        Name = NameOf(node, role),
        Value = ValueOf(node, role),
        Disabled = Attribute(node, "disabled") is not null || Attribute(node, "aria-disabled") == "true",
        Checked = CheckOf(node),
        Selected = Flag(Attribute(node, "aria-selected")),
        Expanded = Flag(Attribute(node, "aria-expanded")),
        Current = Attribute(node, "aria-current") is { } current && current != "false",
        HeadingLevel = headingLevel,
    };

    private static SemanticRole? RoleOf(HtmlNode node, out int headingLevel)
    {
        headingLevel = 0;
        if (Attribute(node, "role") is { } written)
        {
            var role = written.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
            if (role != "heading") return role is not null && AriaRoles.TryGetValue(role, out var known) ? known : null;
            headingLevel = int.TryParse(Attribute(node, "aria-level"), out var level) ? level : 2;
            return SemanticRole.StaticText;
        }

        // A region that says it is live without a role is one too.
        if (Attribute(node, "aria-live") is { } live && live != "off") return SemanticRole.Group;

        switch (node.Tag)
        {
            case "button":
                return SemanticRole.Button;
            case "a":
                return Attribute(node, "href") is not null ? SemanticRole.Link : null;
            case "textarea":
                return SemanticRole.TextField;
            case "input":
                return Attribute(node, "type") switch
                {
                    "checkbox" => SemanticRole.Checkbox,
                    "radio" => SemanticRole.Radio,
                    "range" => SemanticRole.Slider,
                    "button" or "submit" or "reset" or "image" => SemanticRole.Button,
                    _ => SemanticRole.TextField,
                };
            case "img":
                // An empty alt is the page saying the picture says nothing.
                return Attribute(node, "alt") == "" ? null : SemanticRole.Image;
            case { Length: 2 } tag when tag[0] == 'h' && tag[1] is >= '1' and <= '6':
                headingLevel = tag[1] - '0';
                return SemanticRole.StaticText;
            default:
                return null;
        }
    }

    /// <summary>The roles read and then walked into; every other role's subtree is its name.</summary>
    private static bool IsContainer(SemanticRole role) => role is SemanticRole.Group or SemanticRole.Dialog
        or SemanticRole.AlertDialog or SemanticRole.TabBar or SemanticRole.RadioGroup or SemanticRole.Table
        or SemanticRole.Grid;

    /// <summary>The roles ARIA names from what they hold.</summary>
    private static bool NamedFromContent(SemanticRole role) => role is SemanticRole.Button or SemanticRole.Link
        or SemanticRole.Checkbox or SemanticRole.Switch or SemanticRole.Radio or SemanticRole.Tab
        or SemanticRole.MenuItem or SemanticRole.Option or SemanticRole.GridCell or SemanticRole.Cell
        or SemanticRole.ColumnHeader or SemanticRole.RowHeader or SemanticRole.StaticText;

    private string NameOf(HtmlNode node, SemanticRole role)
    {
        // A label an element names by id is read even when it is hidden: the accessible-name rule
        // reads what a reference points at, which is how a page names a dialog by a title it does not
        // show.
        if (Attribute(node, "aria-labelledby") is { } ids)
            return Collapse(string.Join(" ", ids.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Select(id => _byId.TryGetValue(id, out var label) ? TextOf(label, includeHidden: true) : "")));
        if (Attribute(node, "aria-label") is { } label && Collapse(label) is { Length: > 0 } named) return named;
        if (node.Tag == "img" && Attribute(node, "alt") is { } alt) return Collapse(alt);
        if (NamedFromContent(role) && Collapse(TextOf(node)) is { Length: > 0 } content) return content;
        // The last resorts of HTML's name computation (HTML-AAM): an element's title, and for a text
        // field after it, its placeholder, which is how a field without a label is announced.
        if (Attribute(node, "title") is { } title && Collapse(title) is { Length: > 0 } titled) return titled;
        if (node.Tag is "input" or "textarea" && Attribute(node, "placeholder") is { } placeholder)
            return Collapse(placeholder);
        return "";
    }

    private string? ValueOf(HtmlNode node, SemanticRole role) => role switch
    {
        SemanticRole.TextField or SemanticRole.CodeField =>
            node.Tag == "textarea" ? TextOf(node) : Attribute(node, "value") ?? "",
        SemanticRole.Slider or SemanticRole.ProgressIndicator =>
            Attribute(node, "aria-valuetext") ?? Attribute(node, "aria-valuenow") ?? Attribute(node, "value"),
        SemanticRole.ComboBox => Collapse(TextOf(node)),
        _ => null,
    };

    private static SemanticCheck? CheckOf(HtmlNode node)
    {
        if (Attribute(node, "aria-checked") is { } check)
            return check switch
            {
                "true" => SemanticCheck.On,
                "mixed" => SemanticCheck.Mixed,
                _ => SemanticCheck.Off,
            };
        return node.Tag == "input" && Attribute(node, "type") is "checkbox" or "radio"
            ? Attribute(node, "checked") is not null ? SemanticCheck.On : SemanticCheck.Off
            : null;
    }

    /// <summary>
    /// The text a subtree reads as, hidden parts left out unless <paramref name="includeHidden"/>.
    /// Inline elements run together, as they do on the page; a block one stands apart, so two
    /// paragraphs do not read as one word.
    /// </summary>
    private string TextOf(HtmlNode node, bool includeHidden = false)
    {
        if (node.Tag == "#text") return node.TextContent ?? "";
        if (!includeHidden && Hidden(node)) return "";
        var text = string.Concat(node.Children.Select(child => TextOf(child, includeHidden)));
        return node.Tag is "div" or "p" or "li" or "section" or "article" ? $" {text} " : text;
    }

    private bool Hidden(HtmlNode node) =>
        Attribute(node, "aria-hidden") == "true"
        || Attribute(node, "hidden") is not null
        || (node.Tag == "input" && Attribute(node, "type") == "hidden")
        || (Attribute(node, "class") is { } classes
            && classes.Split(' ', StringSplitOptions.RemoveEmptyEntries).Any(name => _styles.HidesAt(name, _width)))
        || InlineHides(Attribute(node, "style"));

    /// <summary>A declaration the sink did not take, left inline: the same two that hide.</summary>
    private static bool InlineHides(string? style) => style is not null
        && style.Split(';').Select(declaration => Whitespace.Replace(declaration, "")).Any(declaration =>
            declaration is "display:none" or "visibility:hidden");

    private static string? Attribute(HtmlNode node, string name) =>
        node.Attributes.TryGetValue(name, out var value) ? value : null;

    private static bool? Flag(string? value) => value switch
    {
        "true" => true,
        "false" => false,
        _ => null,
    };

    private static string Collapse(string? text) => text is null ? "" : Whitespace.Replace(text, " ").Trim();
}
