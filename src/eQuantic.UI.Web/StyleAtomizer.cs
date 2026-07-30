using System.Runtime.CompilerServices;
using System.Text;
using eQuantic.UI.Core;
using eQuantic.UI.Primitives;

namespace eQuantic.UI.Web;

/// <summary>
/// The web half of the style pipeline (docs/STYLE-SEMANTICS-PLAN.md §2): every REGULAR style
/// declaration becomes one ATOMIC CSS RULE, deduplicated app-wide — a style value is immutable, so
/// identical declarations are the same declaration, hashed to the same class name. The 100th card
/// adds zero bytes of CSS and a handful of short class names, instead of a full inline style string.
/// Custom properties (<c>--*</c>) are tier 3 — per-element inputs that stay inline by design.
/// The TypeScript runtime carries a byte-identical twin (<c>style-atomizer.ts</c>): both sides hash
/// the same declaration text to the same class, so SSR markup and client lowering agree by CLASS
/// IDENTITY and hydration never repaints. The twin pair is cross-pinned by a shared fixture.
/// </summary>
public static class StyleAtomizer
{
    /// <summary>
    /// One atomic class per declaration; classes come back SORTED so the joined attribute is
    /// order-independent (the C# realizer and the TS lowering may compute properties in different
    /// orders yet must emit the same class attribute). Theme-sourced colors are rewritten to
    /// <c>var(--eq-color-*, fallback)</c> so the RULES are theme-independent — swapping the theme
    /// swaps variables, never rules.
    /// </summary>
    public static string Atomize(HtmlStyle style, ThemeVarMap vars, StyleSink sink)
    {
        var classes = new List<string>();
        foreach (var declaration in style.EnumerateDeclarations())
        {
            var value = vars.Rewrite(declaration.Value);
            classes.Add(sink.ClassFor(declaration.Key, value));
        }
        classes.Sort(StringComparer.Ordinal);
        return string.Join(" ", classes);
    }

    /// <summary>
    /// Post-pass over a realized tree: converts every element's style object into atomic classes
    /// (collected into <paramref name="sink"/>), leaving inline ONLY the custom-property tail.
    /// This is the single choke point — the 39 per-node style computations upstream stay untouched.
    /// </summary>
    public static void AtomizeTree(HtmlElement element, ThemeVarMap vars, StyleSink sink)
    {
        if (element.Style is { } style)
        {
            var atomic = Atomize(style, vars, sink);
            if (atomic.Length > 0)
                element.ClassName = string.IsNullOrEmpty(element.ClassName)
                    ? atomic
                    : $"{element.ClassName} {atomic}";

            element.Style = style.CustomProperties is { Count: > 0 }
                ? new HtmlStyle { CustomProperties = style.CustomProperties }
                : null;
        }

        // Spec S6: an adaptive gate registers its fixed media blob (idempotent) — the class shows
        // the variant only inside its size-class range (display:contents/none, zero JS).
        if (element is IAdaptiveGated { AdaptiveGate: { } gate })
        {
            sink.AddAdaptiveGate(gate);
            element.ClassName = string.IsNullOrEmpty(element.ClassName) ? gate : $"{element.ClassName} {gate}";
        }

        // Spec S5 pseudo-variant classes come AFTER the base atomic set — the same order the TS
        // lowering appends them (hydration compares the class attribute as one string).
        if (element is IPseudoStyled { PseudoDeclarations: { Count: > 0 } pseudos })
        {
            var classes = new List<string>();
            foreach (var (pseudo, prop, value) in pseudos)
                classes.Add(sink.ClassFor(prop, vars.Rewrite(value), pseudo));
            classes.Sort(StringComparer.Ordinal);
            var joined = string.Join(" ", classes);
            element.ClassName = string.IsNullOrEmpty(element.ClassName) ? joined : $"{element.ClassName} {joined}";
        }

        foreach (var child in element.Children)
        {
            if (child is HtmlElement childElement)
                AtomizeTree(childElement, vars, sink);
        }
    }

    /// <summary>FNV-1a 32-bit over UTF-16 code units, base36 — deterministic, tiny, and trivially
    /// mirrored in TypeScript (charCodeAt sees the same code units).</summary>
    internal static string Hash(string text)
    {
        var hash = 2166136261u;
        foreach (var ch in text)
        {
            hash ^= ch;
            hash *= 16777619u;
        }

        const string digits = "0123456789abcdefghijklmnopqrstuvwxyz";
        Span<char> buffer = stackalloc char[8];
        var i = buffer.Length;
        do
        {
            buffer[--i] = digits[(int)(hash % 36)];
            hash /= 36;
        } while (hash > 0);
        return new string(buffer[i..]);
    }
}

/// <summary>Spec S5 channel: elements carrying pseudo-state declarations for the atomizer pass.</summary>
public interface IPseudoStyled
{
    List<(string Pseudo, string Prop, string Value)> PseudoDeclarations { get; }
}

/// <summary>Spec S6 channel: an adaptive-variant wrapper carrying its size-class GATE.</summary>
public interface IAdaptiveGated
{
    string? AdaptiveGate { get; }
}

/// <summary>
/// The atomic-rule registry of one render (SSR) or one app (client twin): class → declaration,
/// insert-once. <see cref="Css"/> is what the server injects next to the page so hydration sees the
/// exact rules the markup references.
/// </summary>
public sealed class StyleSink
{
    private static readonly AsyncLocal<StyleSink?> _ambient = new();

    /// <summary>
    /// The render-scoped ambient sink: the SSR pipeline arms one around a page render so every
    /// <see cref="VisualNodeComponent"/> INSIDE the tree (Core pages compose bridges internally)
    /// contributes to a single per-page rule set. Null outside an SSR render.
    /// </summary>
    public static StyleSink? Ambient
    {
        get => _ambient.Value;
        set => _ambient.Value = value;
    }

    private readonly Dictionary<string, string> _rules = new();

    public string ClassFor(string property, string value) => ClassFor(property, value, pseudo: null);

    /// <summary>Spec S5: a PSEUDO-VARIANT rule of the same atomic family — the declaration only
    /// applies under <paramref name="pseudo"/> (":hover", ":focus-visible"). The pseudo is part of
    /// the hash, so hover and base variants of the same declaration are distinct classes.</summary>
    public string ClassFor(string property, string value, string? pseudo)
    {
        var declaration = $"{property}:{value}";
        var className = $"eq-{StyleAtomizer.Hash(pseudo is null ? declaration : $"{pseudo}|{declaration}")}";
        _rules.TryAdd(className, pseudo is null ? declaration : $"{pseudo}\u0001{declaration}");
        return className;
    }

    public bool IsEmpty => _rules.Count == 0;

    /// <summary>Spec S6: the FIXED gate rules — visible ranges follow the AdaptiveNode fallback
    /// chain (a variant shows until the next DECLARED variant takes over). Raw blobs, one per gate
    /// class, byte-identical to the TS twin.</summary>
    public void AddAdaptiveGate(string gate) => _rules.TryAdd(gate, "\u0002" + AdaptiveGates.Css(gate));

    /// <summary>Every collected rule, sorted by class name (deterministic output for tests/caching).</summary>
    public string Css
    {
        get
        {
            var css = new StringBuilder();
            foreach (var rule in _rules.OrderBy(r => r.Key, StringComparer.Ordinal))
            {
                if (rule.Value.StartsWith('\u0002')) { css.Append(rule.Value[1..]); continue; }
                var split = rule.Value.IndexOf('\u0001');
                if (split >= 0)
                    css.Append('.').Append(rule.Key).Append(rule.Value[..split])
                       .Append('{').Append(rule.Value[(split + 1)..]).Append('}');
                else
                    css.Append('.').Append(rule.Key).Append('{').Append(rule.Value).Append('}');
            }
            return css.ToString();
        }
    }
}

/// <summary>Spec S6: the size-class gate vocabulary — class names and their media blobs, shared
/// verbatim by the C# realizer and the TS lowering. Ranges encode the fallback chain: a variant is
/// visible from its own threshold until the next DECLARED variant's threshold.</summary>
public static class AdaptiveGates
{
    /// <summary>Compact hidden from 600dp (a Medium variant exists).</summary>
    public const string CompactUntilMedium = "eq-vc6";

    /// <summary>Compact hidden from 840dp (no Medium — Compact serves until Expanded).</summary>
    public const string CompactUntilExpanded = "eq-vc8";

    /// <summary>Medium from 600dp, hidden from 840dp (an Expanded variant exists).</summary>
    public const string MediumUntilExpanded = "eq-vm8";

    /// <summary>Medium from 600dp, open-ended (no Expanded).</summary>
    public const string MediumOpen = "eq-vm";

    /// <summary>Expanded from 840dp.</summary>
    public const string Expanded = "eq-vx";

    public static string Css(string gate) => gate switch
    {
        CompactUntilMedium => ".eq-vc6{display:contents}@media (min-width: 600px){.eq-vc6{display:none}}",
        CompactUntilExpanded => ".eq-vc8{display:contents}@media (min-width: 840px){.eq-vc8{display:none}}",
        MediumUntilExpanded => ".eq-vm8{display:none}@media (min-width: 600px) and (max-width: 839.98px){.eq-vm8{display:contents}}",
        MediumOpen => ".eq-vm{display:none}@media (min-width: 600px){.eq-vm{display:contents}}",
        Expanded => ".eq-vx{display:none}@media (min-width: 840px){.eq-vx{display:contents}}",
        _ => throw new ArgumentOutOfRangeException(nameof(gate), gate, "Unknown adaptive gate."),
    };
}

/// <summary>
/// Reverse map of the ACTIVE theme's color tokens to their <c>--eq-color-*</c> variables (the names
/// <see cref="PhotonCssGenerator"/> emits). Values arrive at the atomizer already resolved to
/// <c>light-dark()</c> strings; rewriting them to <c>var(--eq-color-x, light-dark(…))</c> makes the
/// atomic rules THEME-INDEPENDENT (the fallback keeps them correct even with no token stylesheet).
/// Built once per theme instance; when two roles resolve to the same color the FIRST registration
/// wins — registration order is fixed and mirrored by the TS twin, so both sides pick the same var.
/// </summary>
public sealed class ThemeVarMap
{
    private static readonly ConditionalWeakTable<IAppTheme, ThemeVarMap> Cache = new();

    // Ordered list, not a dictionary: rewrite order IS the contract (first registration wins when
    // two roles share a color), and the TS twin replays the identical order.
    private readonly List<(string TokenCss, string VarName)> _entries = new();
    private readonly HashSet<string> _seen = new();

    public static ThemeVarMap For(IAppTheme theme) => Cache.GetValue(theme, Build);

    public string Rewrite(string value)
    {
        // Fast path: no token color in the value.
        if (!value.Contains("light-dark(") && !value.StartsWith('#')) return value;

        // Exact-value declarations (background-color, color, border shorthand tail…) and composite
        // values (gradients, borders) both resolve by substring replacement of known token strings.
        foreach (var (tokenCss, varName) in _entries)
        {
            if (value == tokenCss) return $"var({varName}, {tokenCss})";
            if (value.Contains(tokenCss))
                value = value.Replace(tokenCss, $"var({varName}, {tokenCss})");
        }
        return value;
    }

    private static ThemeVarMap Build(IAppTheme theme)
    {
        var map = new ThemeVarMap();
        // Surfaces/chrome — the exact names PhotonCssGenerator emits, in its emission order.
        map.Register(theme.Background, "background");
        map.Register(theme.Surface, "surface");
        map.Register(theme.SurfaceSubtle, "surface-subtle");
        map.Register(theme.SurfaceHighlight, "surface-highlight");
        map.Register(theme.Border, "border");
        map.Register(theme.BorderStrong, "border-strong");
        map.Register(theme.TextPrimary, "text-primary");
        map.Register(theme.TextSecondary, "text-secondary");
        map.Register(theme.TextMuted, "text-muted");
        map.Register(theme.TextInverse, "text-inverse");
        map.Register(theme.FocusRing, "focus");
        map.Register(theme.LinkColor, "link");
        map.Register(theme.Scrim, "scrim");

        foreach (var variant in new[]
                 {
                     Variant.Primary, Variant.Secondary, Variant.Destructive,
                     Variant.Success, Variant.Warning, Variant.Info, Variant.Tertiary,
                 })
        {
            var name = variant.ToString().ToLowerInvariant();
            var colors = theme.Colors(variant);
            map.Register(colors.Base, $"{name}-base");
            map.Register(colors.OnBase, $"{name}-on");
            map.Register(colors.Pressed, $"{name}-pressed");
            map.Register(colors.Subtle, $"{name}-subtle");
            map.Register(colors.OnSubtle, $"{name}-on-subtle");
        }

        return map;
    }

    private void Register(ColorToken token, string name)
    {
        var value = TokenCss.Value(token);
        if (_seen.Add(value))
            _entries.Add((value, $"--eq-color-{name}"));
    }
}
