namespace eQuantic.UI.Web;

/// <summary>
/// How a <see cref="Primitives.MergeSemantics"/> is written into the markup it lowered (TS twin:
/// <c>mergeReading</c>), so a reader hears the subtree as ONE stop, as Photon's semantics walk says it.
///
/// <para>
/// The subtree is read in order and cut into PARTS, the pieces a reader would have stopped on: an element
/// holding only text, an element named by a label, an element with a role of its own. A layout element
/// around them is walked into, so the parts of a row are named apart and a reader hears a word between
/// them, as Photon joins its stops. What a reader already passes over is no part. Each part with something
/// to say is named by an id and hidden, and the one stop names itself by those ids. When a control is in the subtree, the first one is the stop: its
/// <c>aria-labelledby</c> lists the parts and itself, in reading order, so its role, its value and its
/// states stay its own and its name is the whole row. With no control the host is the stop, a
/// <c>role="group"</c> named the same way. A later control is left as it is, a stop of its own, as on
/// Photon.
/// </para>
///
/// <para>
/// THE NAME IS THE BROWSER'S TO COMPUTE, never a string written here. A part named by
/// <c>aria-labelledby</c> is read however hidden, so what it says reaches the name while it is no stop of
/// its own, and when its text changes the name changes with it: nothing here goes stale. Only the parts
/// themselves are found from the markup, which is why the browser's lowering marks every element it walks
/// as reached into, so a component inside is never lowered again without the merge around it.
/// </para>
///
/// <para>
/// The ids are the merge's path scope (<see cref="LoweringPath.Scope"/>) and an index in reading order,
/// the same on the server's first pass and after hydration. An element that has an id keeps it.
/// </para>
/// </summary>
internal static class MergedReading
{
    /// <summary>The roles that group what they hold, walked into like a layout element: a reader reads them
    /// and goes on into their contents, so their contents are the parts.</summary>
    private static readonly HashSet<string> ContainerRoles = new(StringComparer.Ordinal)
    {
        "group", "status", "log", "alert", "list", "listbox", "table", "grid", "row", "rowgroup", "tablist",
        "radiogroup", "dialog", "alertdialog", "tree", "region", "none", "presentation",
    };

    /// <summary>The ARIA roles of the controls a merge's stop becomes, Photon's
    /// <c>SemanticsVisitor.IsControl</c> written as the web writes them.</summary>
    private static readonly HashSet<string> ControlRoles = new(StringComparer.Ordinal)
    {
        "button", "link", "textbox", "searchbox", "slider", "checkbox", "switch", "gridcell", "radio", "tab",
        "menuitem", "menuitemcheckbox", "menuitemradio", "option", "combobox", "treeitem",
    };

    /// <summary>Writes the merged reading of <paramref name="child"/>, lowered under <paramref name="host"/>.</summary>
    public static void Merge(RealizedElement host, IComponent child, string idPrefix)
    {
        var control = FirstControl(child);
        var ids = new List<string>();
        var next = 0;
        Collect(child, control, ids, idPrefix, ref next);
        if (control is not null)
        {
            // Only its own name: nothing to join, so the control is read as it was.
            if (ids.Count > 1) Set(control, "aria-labelledby", string.Join(" ", ids));
        }
        else if (ids.Count > 0)
        {
            Set(host, "role", "group");
            Set(host, "aria-labelledby", string.Join(" ", ids));
        }
    }

    private static void Collect(IComponent node, RealizedElement? control, List<string> ids, string prefix, ref int next)
    {
        if (node is not RealizedElement element || Hidden(element)) return;
        if (ReferenceEquals(element, control))
        {
            ids.Add(IdOf(element, $"{prefix}-c"));
            return;
        }
        if (IsControl(element)) return;
        if (!ReadsAsOne(element))
        {
            foreach (var child in element.Children) Collect(child, control, ids, prefix, ref next);
            return;
        }
        if (!SaysSomething(element)) return;
        ids.Add(IdOf(element, $"{prefix}-{next++}"));
        Set(element, "aria-hidden", "true");
    }

    /// <summary>Whether an element is one part: it holds only text, it is named by a label, it is an image, or
    /// it has a role of its own that does not group what it holds.</summary>
    private static bool ReadsAsOne(RealizedElement element)
    {
        if (Attribute(element, "role") is { } role)
            return !(role.Split(' ', StringSplitOptions.RemoveEmptyEntries) is [var first, ..] && ContainerRoles.Contains(first));
        if (Attribute(element, "aria-label") is { Length: > 0 } || Attribute(element, "alt") is { Length: > 0 }) return true;
        return element.Tag == "img" || (element.Children.Count == 0 && !string.IsNullOrEmpty(element.InnerHtml));
    }

    /// <summary>Whether a part has a word for a reader: text, or an element named by a label.</summary>
    private static bool SaysSomething(RealizedElement element)
    {
        if (!string.IsNullOrWhiteSpace(element.InnerHtml)) return true;
        if (Attribute(element, "aria-label") is { Length: > 0 } || Attribute(element, "alt") is { Length: > 0 }) return true;
        foreach (var child in element.Children)
            if (child is RealizedElement inner && !Hidden(inner) && SaysSomething(inner)) return true;
        return false;
    }

    private static RealizedElement? FirstControl(IComponent node)
    {
        if (node is not RealizedElement element || Hidden(element)) return null;
        if (IsControl(element)) return element;
        foreach (var child in element.Children)
            if (FirstControl(child) is { } found) return found;
        return null;
    }

    /// <summary>An element a person operates: its written role is a control's, or, with none, its tag is
    /// one (<c>button</c>, <c>a</c> with <c>href</c>, a visible <c>input</c>, <c>textarea</c>,
    /// <c>select</c>).</summary>
    internal static bool IsControl(RealizedElement element)
    {
        if (Attribute(element, "role") is { } role)
            return role.Split(' ', StringSplitOptions.RemoveEmptyEntries) is [var first, ..] && ControlRoles.Contains(first);
        return element.Tag switch
        {
            "button" or "textarea" or "select" => true,
            "a" => Attribute(element, "href") is not null,
            "input" => Attribute(element, "type") != "hidden",
            _ => false,
        };
    }

    private static bool Hidden(RealizedElement element) =>
        Attribute(element, "aria-hidden") == "true" || Attribute(element, "hidden") is not null;

    private static string IdOf(RealizedElement element, string fresh)
    {
        if (Attribute(element, "id") is { Length: > 0 } id) return id;
        Set(element, "id", fresh);
        return fresh;
    }

    /// <summary>What the element will render for <paramref name="name"/>: the verbatim attribute, which
    /// rendering lays over the typed one, or the typed one.</summary>
    private static string? Attribute(RealizedElement element, string name)
    {
        if (element.RawAttributes?.TryGetValue(name, out var raw) == true) return raw;
        return name switch
        {
            "id" => element.Id,
            "role" => element.Role,
            "aria-hidden" => element.AriaHidden is { } hidden ? (hidden ? "true" : "false") : null,
            "hidden" => element.Hidden == true ? "true" : null,
            "aria-label" => element.AriaLabel,
            _ => null,
        };
    }

    private static void Set(RealizedElement element, string name, string value)
    {
        element.RawAttributes ??= new Dictionary<string, string>();
        element.RawAttributes[name] = value;
    }
}
