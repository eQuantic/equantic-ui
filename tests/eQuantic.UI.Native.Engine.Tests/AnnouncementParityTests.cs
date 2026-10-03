using eQuantic.UI.Components;
using eQuantic.UI.Native.Components;
using eQuantic.UI.Native.Engine;
using eQuantic.UI.Primitives;
using eQuantic.UI.Web;
using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// The same component announces the same ROLE, NAME, STATE and ORDER on the web and on Photon, for
/// the roles #500 and #501 gave the native tree: a tab bar and its tabs, a radio group and its
/// radios, a combobox and its options, a menu's items, a dialog and an alert dialog.
/// <para>
/// The web writes ARIA inline and Photon walks a semantics tree, and nothing compared the two:
/// that is how a Tabs was a tablist of named tabs in the browser and one nameless slider on every
/// native platform, with every suite green. This is the slice of #339's fixture these roles need.
/// Each case is built twice, lowered once to the DOM and laid out once by a host, and the two
/// announcements, the DOM's in document order and the tree's in reading order, must be one list.
/// #339 widens it to every component.
/// </para>
/// <para>
/// A NAME is the author's label or, for a control, the words inside it, the rule both realizers
/// apply: on Photon <c>Pressable.Label ?? TextWithin</c>, on the web the same label as
/// <c>aria-label</c> and the same words as the content. A container takes no name from what it
/// holds, on either target.
/// </para>
/// </summary>
public class AnnouncementParityTests
{
    private static readonly IAppTheme Theme = PhotonTheme.Instance;

    /// <summary>What one stop says: its role in the tree's words, its name, and the one state its
    /// role carries, spelled the way ARIA spells it.</summary>
    private readonly record struct Announcement(SemanticRole Role, string Name, string? State);

    /// <summary>The ARIA roles compared, and what the semantics tree calls each.</summary>
    private static readonly Dictionary<string, SemanticRole> AriaRoles = new()
    {
        ["tablist"] = SemanticRole.TabBar,
        ["tab"] = SemanticRole.Tab,
        ["radiogroup"] = SemanticRole.RadioGroup,
        ["radio"] = SemanticRole.Radio,
        ["combobox"] = SemanticRole.ComboBox,
        ["option"] = SemanticRole.Option,
        ["menuitem"] = SemanticRole.MenuItem,
        ["dialog"] = SemanticRole.Dialog,
        ["alertdialog"] = SemanticRole.AlertDialog,
    };

    /// <summary>The roles that take no name from what they hold, ARIA's rule and the walk's.</summary>
    private static readonly HashSet<string> Containers = ["tablist", "radiogroup", "dialog", "alertdialog"];

    /// <summary>The cases, each with whether its one disclosure is opened before the reading. An
    /// open case is where a layer can appear on one target and not the other.</summary>
    public static TheoryData<string, bool> Cases => new()
    {
        { "tabs", false },
        { "radio-group", false },
        { "segmented-control", false },
        { "select", false },
        { "select", true },
        { "time-picker", false },
        { "disabled-select", false },
        { "disabled-time-picker", false },
        { "menu", true },
        { "date-picker", true },
        { "dialog", false },
        { "alert-dialog", false },
    };

    /// <summary>A FRESH component per target: a stateful one keeps what a press did to it.</summary>
    private static VisualNode Build(string name) => name switch
    {
        "tabs" => new Tabs(["Overview", "Activity", "Settings"], 1, _ => { }),
        "radio-group" => new RadioGroup(["Standard", "Express"], 1, _ => { }, "Shipping"),
        "segmented-control" => new SegmentedControl(["All", "Income"], 0, _ => { }),
        "select" => new Select(["Lisbon", "Porto"], 0, _ => { }),
        "time-picker" => new TimePicker(new TimeOnly(9, 30), _ => { }, label: "Arrival"),
        "disabled-select" => new Select(["Lisbon", "Porto"], 0, _ => { }) { Disabled = true },
        "disabled-time-picker" => new TimePicker(new TimeOnly(9, 30), _ => { }, label: "Arrival") { Disabled = true },
        "menu" => new Menu(new Button("More"), [new MenuItem("Rename"), new MenuItem("Delete")], _ => { }),
        "date-picker" => new DatePicker(new DateOnly(2026, 7, 17), _ => { }, label: "Departure"),
        "dialog" => Page(new Dialog("Sign out?", "You can sign back in anytime.",
            [new DialogAction("Stay", null, Variant.Ghost), new DialogAction("Sign out")])),
        "alert-dialog" => Page(new Dialog("Delete card?", "It cannot be undone.",
            [new DialogAction("Keep", null, Variant.Ghost), new DialogAction("Delete", null, Variant.Destructive)])),
        _ => throw new ArgumentOutOfRangeException(nameof(name), name, "not a case"),
    };

    private static VisualNode Page(VisualNode layer)
    {
        var page = new Column(gap: Space.S2) { Width = SizeValue.Fill };
        page.Add(new Text("Cards", TypeRole.Title));
        page.Add(layer);
        return page;
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void TheWebAndPhotonAnnounceTheSameRolesNamesStatesAndOrder(string name, bool open)
    {
        var web = OnTheWeb(Build(name), open);
        var photon = OnPhoton(Build(name), open);

        web.Should().NotBeEmpty("a case that announces none of the compared roles compares nothing");
        photon.Should().Equal(web,
            $"'{name}' has to say the same thing to a screen reader on every target, in the same order");
    }

    // ---- the web ---------------------------------------------------------------------------------

    private static List<Announcement> OnTheWeb(VisualNode node, bool open)
    {
        var tree = WebRealizer.Lower(node, Theme).Render();
        if (open)
        {
            // The disclosure's own click, as the parity fixture presses one: the handler IS the
            // Pressable's action, and lowering the same component again reads the state it moved.
            var trigger = Elements(tree).Single(element => element.Attributes.ContainsKey("aria-expanded")
                && element.Events.Keys.Any(key => key is "click" or "onclick"));
            var press = trigger.Events.First(pair => pair.Key is "click" or "onclick").Value;
            if (press is Action action) action();
            else press.DynamicInvoke();
            tree = WebRealizer.Lower(node, Theme).Render();
        }

        return Elements(tree)
            .Where(element => element.Attributes.GetValueOrDefault("role") is { } aria && AriaRoles.ContainsKey(aria))
            .Select(element =>
            {
                var aria = element.Attributes["role"]!;
                var name = element.Attributes.GetValueOrDefault("aria-label")
                           ?? (Containers.Contains(aria) ? "" : TextOf(element).Trim());
                return new Announcement(AriaRoles[aria], name, StateOf(element));
            })
            .ToList();
    }

    /// <summary>Every element, in document order.</summary>
    private static IEnumerable<HtmlNode> Elements(HtmlNode node)
    {
        yield return node;
        foreach (var child in node.Children)
            foreach (var element in Elements(child))
                yield return element;
    }

    private static string TextOf(HtmlNode node) =>
        node.TextContent ?? string.Concat(node.Children.Select(TextOf));

    private static string? StateOf(HtmlNode element) =>
        element.Attributes.GetValueOrDefault("aria-checked") is { } check ? $"checked={check}"
        : element.Attributes.GetValueOrDefault("aria-selected") is { } pick ? $"selected={pick}"
        : element.Attributes.GetValueOrDefault("aria-expanded") is { } expanded ? $"expanded={expanded}"
        : null;

    // ---- Photon ------------------------------------------------------------------------------------

    private static List<Announcement> OnPhoton(VisualNode node, bool open)
    {
        var host = new PhotonHost(node, Theme, ThemeMode.Light, 400, 800);
        host.RenderFrame(new DisplayListBuilder());
        if (open)
        {
            host.ActivatePath(host.Semantics().Single(stop => stop.Expanded is not null).Path).Should().BeTrue();
            host.RenderFrame(new DisplayListBuilder(), 16);
        }

        var compared = AriaRoles.Values.ToHashSet();
        return host.Semantics()
            .Where(stop => compared.Contains(stop.Role))
            .Select(stop => new Announcement(stop.Role, stop.Label, StateOf(stop)))
            .ToList();
    }

    private static string? StateOf(SemanticNode stop) =>
        stop.Checked is { } check
            ? $"checked={check switch { SemanticCheck.On => "true", SemanticCheck.Off => "false", _ => "mixed" }}"
        : stop.Selected is { } pick ? $"selected={(pick ? "true" : "false")}"
        : stop.Expanded is { } expanded ? $"expanded={(expanded ? "true" : "false")}"
        : null;
}
