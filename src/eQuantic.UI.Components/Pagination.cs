using eQuantic.UI.Primitives;

namespace eQuantic.UI.Components;

/// <summary>
/// Page navigation for a set too large to scroll (spec B19) — the pointer counterpart to the
/// <see cref="PageIndicator"/> dot row, which is a readout for a handful of pages.
/// <para>
/// The window of numbers is the component's problem, not the caller's: 70 pages cannot be 70
/// buttons, so the control shows the first, the last, a run around the current page, and an ellipsis
/// where it skipped. That elision is computed here so every table in an app elides the same way.
/// </para>
/// <para>
/// Previous and Next DISABLE at the ends rather than vanishing — a control that disappears takes
/// the row's layout with it, and the user loses the place they were aiming at.
/// </para>
/// </summary>
public sealed class Pagination : StatelessComponent
{
    /// <summary>How many numbers surround the current page before the run is elided.</summary>
    public const int Window = 1;

    /// <summary>Up to this many pages, every number is shown — eliding would save nothing.</summary>
    public const int AlwaysShow = 7;

    private const float Cell = 30;

    public Pagination(int pageCount, int currentPage, Action<int>? onChanged = null)
    {
        PageCount = pageCount;
        CurrentPage = currentPage;
        OnChanged = onChanged;
    }

    /// <summary>Total pages; 1-based numbering throughout, because that is what users read.</summary>
    public int PageCount { get; init; }
    public int CurrentPage { get; init; }
    public Action<int>? OnChanged { get; init; }

    public override VisualNode Build(ComponentContext context)
    {
        var theme = context.Theme;
        var current = Math.Clamp(CurrentPage, 1, Math.Max(1, PageCount));

        var numbers = new Row(gap: 3) { Cross = CrossAlign.Center };
        var previous = 0;
        foreach (var page in Pages(PageCount, current))
        {
            if (previous > 0 && page > previous + 1) numbers.Add(Ellipsis(theme));
            numbers.Add(Number(theme, page, page == current));
            previous = page;
        }

        var row = new Row(gap: Space.S2) { Cross = CrossAlign.Center };
        row.Add(Step("Previous", current > 1, () => OnChanged?.Invoke(current - 1)));
        row.Add(numbers);
        row.Add(Step("Next", current < PageCount, () => OnChanged?.Invoke(current + 1)));
        return row;
    }

    /// <summary>
    /// Which page numbers survive the elision: always the first and the last, plus
    /// <see cref="Window"/> either side of the current one. Pure and static, so a test can pin the
    /// windowing without building a tree.
    /// </summary>
    public static int[] Pages(int pageCount, int currentPage)
    {
        if (pageCount <= 0) return [];
        if (pageCount <= AlwaysShow)
        {
            var all = new int[pageCount];
            for (var page = 1; page <= pageCount; page++) all[page - 1] = page;
            return all;
        }

        var current = Math.Clamp(currentPage, 1, pageCount);
        var pages = new List<int> { 1 };
        for (var page = current - Window; page <= current + Window; page++)
            if (page > 1 && page < pageCount && !pages.Contains(page)) pages.Add(page);
        if (pageCount > 1) pages.Add(pageCount);

        var ordered = new int[pages.Count];
        for (var i = 0; i < pages.Count; i++) ordered[i] = pages[i];
        return ordered;
    }

    private VisualNode Number(IAppTheme theme, int page, bool current)
    {
        var centered = new Row(gap: 0)
        {
            Width = SizeValue.Fill,
            Height = SizeValue.Fill,
            Main = MainAlign.Center,
            Cross = CrossAlign.Center,
        };
        centered.Add(new Text(page.ToString(), TypeRole.Caption,
            current ? theme.Colors(Variant.Primary).OnBase : theme.TextSecondary, maxLines: 1)
        {
            Tabular = true,
        });

        var box = new Box(new BoxStyle
        {
            Width = Cell,
            Height = Cell,
            Background = current ? theme.Colors(Variant.Primary).Base : null,
            CornerRadius = new CornerRadii(theme.Shape(ShapeScale.Small)),
            Hover = current ? null : new StyleDiff { Background = theme.SurfaceSubtle },
            Transition = TransitionSpec.Of(StyleChannels.Colors, Motion.Press),
        }, centered);

        return new Pressable(box, current ? null : () => OnChanged?.Invoke(page))
        {
            Label = $"Page {page}",
            Selected = current,
        };
    }

    /// <summary>The skipped run. Not a button — there is no single page it could mean.</summary>
    private static VisualNode Ellipsis(IAppTheme theme)
    {
        var centered = new Row(gap: 0)
        {
            Width = SizeValue.Fill,
            Height = SizeValue.Fill,
            Main = MainAlign.Center,
            Cross = CrossAlign.Center,
        };
        centered.Add(new Text("…", TypeRole.Caption, theme.TextMuted, maxLines: 1));
        return new Box(new BoxStyle { Width = Cell, Height = Cell }, centered);
    }

    /// <summary>Previous / Next. Disabled at the ends — never removed, or the row reflows.</summary>
    private static VisualNode Step(string label, bool enabled, Action onPressed) =>
        new Button(label, Variant.Outline, SizeVariant.Small)
        {
            Disabled = !enabled,
            OnPressed = onPressed,
        };
}
