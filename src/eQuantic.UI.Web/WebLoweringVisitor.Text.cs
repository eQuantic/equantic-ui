using System.Linq;

using eQuantic.UI.Primitives;

namespace eQuantic.UI.Web;

/// <summary>
/// Text and the editable surfaces. <c>Text</c> is the single largest arm here — the type ramp, the
/// runs, the clamp and the gradient ink all resolve in it — and the two surfaces are the pair whose
/// SSR shape was settled one at a time: <c>SheetSurface</c> writes, and <c>CodeSurface</c> still
/// does not and says why at its own arm.
/// </summary>
internal sealed partial class WebLoweringVisitor
{
    /// <summary>
    /// Spec B9/B10 primitive: a REAL chrome-less &lt;input&gt; — the browser owns caret/selection/
    /// IME. The type role rides the generated .eq-type-* class; color/reset styles are inline;
    /// outline and ::placeholder mechanics live in the generated stylesheet (.eq-entry). The
    /// composing component owns the container chrome. Handlers attach on the client (lowering.ts —
    /// SSR emits no events; hydration wires them). The entry lowers inside a STABLE
    /// .eq-field shell that always carries the sr-only description twin (.eq-desc) — presence
    /// flipping with the description would REPLACE the input and drop focus mid-edit.
    /// </summary>
    private HtmlElement LowerTextEntry(TextEntry entry)
    {
        // A multi-line entry IS a textarea — its value is CONTENT, not an attribute, so SSR carries
        // it the way the parser expects; `rows` is the authored line count.
        var multiline = entry.Lines > 1;
        var element = new RealizedElement(multiline ? "textarea" : "input")
        {
            ClassName = $"eq-entry eq-type-{entry.Role.ToString().ToLowerInvariant()}",
            Style = new HtmlStyle
            {
                // A field takes the keyboard and the pointer, and says so: `none` inherits, and
                // any row above it may now be transparent.
                PointerEvents = "auto",
                Width = "100%",
                Padding = "0",
                Background = "none",
                Border = "none",
                Color = TokenCss.Value(_context.Theme.TextPrimary),
                Resize = multiline ? "vertical" : null,
            },
            RawAttributes = multiline
                ? new Dictionary<string, string> { ["rows"] = entry.Lines.ToString(System.Globalization.CultureInfo.InvariantCulture) }
                : new Dictionary<string, string>
                {
                    ["type"] = entry.Obscure ? "password" : "text",
                    ["value"] = entry.Value,
                },
        };
        if (multiline) element.InnerHtml = entry.Value;
        if (entry.Placeholder is { } placeholder) element.RawAttributes["placeholder"] = placeholder;
        // The accessible NAME — a placeholder vanishes under text, so it never substitutes for one.
        if (entry.Label is { Length: > 0 } accessibleName) element.RawAttributes["aria-label"] = accessibleName;
        if (entry.Disabled) element.RawAttributes["disabled"] = "";
        // NOT DECLARED HERE, and it is a gap rather than a decision: the client twin installs an
        // `input` handler and this side installs nothing, so the parity fixture — which is
        // generated from THIS tree — cannot notice the client losing one. Exactly the hole the
        // grid's keydown had before #12, except that one could be closed in an afternoon because
        // `OnKeyDown` already existed on HtmlElement. `OnInput` is in the event-name map with no
        // property behind it, so closing this means a Core API addition and a hydration story for
        // the value it carries. Filed, not smuggled into a component change.
        // The attribute alone only acts on the initial parse; the client lowering also focuses on
        // mount, which is what a dialog opened later needs.
        if (entry.Autofocus) element.RawAttributes["autofocus"] = "";
        if (entry.Invalid) element.RawAttributes["aria-invalid"] = "true";

        // The description twin: sr-only, and a polite live region — present (empty) from the first
        // paint so a later error swap ANNOUNCES instead of merely recoloring. The id is the
        // content's FNV hash, the atomizer's identity rule — deterministic across SSR and client,
        // no per-render counter (identical captions share one id; the referenced text is the same).
        var description = new RealizedElement("span")
        {
            ClassName = "eq-desc",
            RawAttributes = new Dictionary<string, string> { ["aria-live"] = "polite" },
        };
        if (entry.Description is { Length: > 0 } caption)
        {
            var descriptionId = $"eq-desc-{StyleAtomizer.Hash(caption)}";
            description.RawAttributes["id"] = descriptionId;
            description.InnerHtml = caption;
            element.RawAttributes["aria-describedby"] = descriptionId;
        }

        var host = new RealizedElement("div") { ClassName = "eq-field" };
        host.Children.Add(element);
        host.Children.Add(description);
        return host;
    }
    /// <summary>
    /// The sheet's SKELETON, server-side. It rendered as an empty span to anything that does not run
    /// scripts — no arm here, so `_ => null` caught it, from the day it shipped (d8be2bd6).
    ///
    /// <para>
    /// <c>user-select: none</c> because a drag on a grid extends the SHEET's selection, and the
    /// browser's native text sweep would paint over the band the component draws.
    /// </para>
    /// </summary>
    private HtmlElement LowerSheetSurface(
        SheetSurface sheet, bool? horizontalAxis)
    {
        var attributes = new Dictionary<string, string>
        {
            ["tabindex"] = "0",
            ["role"] = "grid",
        };
        if (sheet.Label is { Length: > 0 } label) attributes["aria-label"] = label;

        var sheetFills = Fills(sheet.Child);
        var sheetCap = CapsAt(sheet.Child);
        var element = new RealizedElement("div")
        {
            Style = new HtmlStyle
            {
                // FIT-CONTENT, not nothing: this host CARRIES THE ROLE, so its box is the bounds a
                // reader outlines for touch exploration and the browser draws the focus ring on —
                // and this one is a tab stop, so that ring is visible. A bare block div stretches to
                // the container while the sheet keeps its own size, and the two stop describing the
                // same thing; on Photon `MeasureWrapper` gives the wrapper exactly the child's
                // bounds. The third instance of #239's rule, and the one that needed the width
                // contract below before it could be stated at all (#241).
                Width = sheetFills.Width ? "100%" : "fit-content",
                // The child's cap comes THROUGH: a wrapper that takes the width and drops the
                // maximum is the half-contract that made the Link diverge once already.
                MaxWidth = Size(sheetCap),
                Height = sheetFills.Height ? "100%" : null,
                PointerEvents = "auto",
                Outline = "none",
                UserSelect = "none",
            },
            RawAttributes = attributes,
        };
        // The inherited axis travels THROUGH, as it does in the client twin: a Spacer inside a
        // sheet inside a Row needs to know which way the row runs, and `null` makes it lower to
        // nothing on the server while the browser renders it. Found in review.
        if (Lower(sheet.Child, horizontalAxis) is { } child) element.Children.Add(child);
        return element;
    }
    private HtmlElement LowerText(Text text)
    {
        // The face can come from the NODE (this text is code) or from the STYLE (this ROLE is
        // code) — the native side merges the two into the style, and so does this.
        // Only what the NODE said, like the face beside it: a mono ROLE rides its `.eq-type-*`
        // class (TokenCss), because the client's lowering cannot read the theme's type scale and
        // anything SSR emits inline from it is dropped on the first client re-render.
        var mono = text.Mono || text.StyleOverride?.Mono == true;
        // The slant reads the same two places for the same reason: a role may BE italic (a
        // theme's caption), and a node may slant a paragraph of an upright role.
        var italic = text.Italic || text.StyleOverride?.Italic == true;
        // Only what the NODE named. A ROLE's face rides its `.eq-type-*` class instead (TokenCss),
        // because the client's lowering cannot read the theme's type scale and an inline role face
        // would be dropped on the first client re-render — SSR showing the brand and hydration
        // showing the system font is the hydration mismatch, not a cosmetic difference.
        var face = FaceName.Usable(text.StyleOverride?.Family);
        // The OUTLINE, not the type scale: `h1`–`h6` when the author placed this text in the
        // document's structure, and a span when they did not. The heading's own UA margin and
        // size are cancelled in the token sheet (`.eq-type-*` owns the size), so choosing a level
        // moves nothing on screen — which is the point, since the level is a semantic decision and
        // a designer must not have to pay for it in layout.
        var element = new RealizedElement(text.HeadingLevel > 0 ? $"h{text.HeadingLevel}" : "span")
        {
            ClassName = $"eq-type-{text.Role.ToString().ToLowerInvariant()}",
            InnerHtml = text.Spans is null ? text.Content : null,
            Style = new HtmlStyle
            {
                Color = TokenCss.Value(text.Color ?? _context.Theme.TextPrimary),
                // Line alignment inside the paragraph: wrapped lines of a centered headline must
                // center too — container alignment only places the block.
                TextAlign = text.Align switch
                {
                    TextAlignment.Center => TextAlign.Center,
                    TextAlignment.End => TextAlign.End,
                    _ => null,
                },
                // Authored \n is a HARD break (the designed headline's line turns) — pre-line
                // keeps normal wrapping between them.
                // Mono text is CODE (indentation survives); plain text only keeps its newlines.
                // PlainContent, not Content: a paragraph built from RUNS has an empty Content, so
                // reading the field instead of what the node says lost the break entirely — the
                // headline ran on in one line and nothing said why.
                WhiteSpace = mono ? "pre-wrap"
                    : text.PlainContent.Contains('\n') ? "pre-line" : null,
                FontFamily = face is { Length: > 0 } named ? TokenCss.Face(named, mono)
                    : mono ? TokenCss.MonoStack : null,
                FontVariantNumeric = text.Tabular ? "tabular-nums" : null,
                FontStyle = italic ? "italic" : null,
                // Spec S6: recolors glide (the design's transition-colors on nav labels/links).
                Transition = text.Transition is { } transition ? TokenCss.Transition(transition) : null,
            },
        };

        // RICH runs: each run flows inline with its own color/mono face; wrapping stays
        // paragraph-level because the children are inline spans.
        if (text.Spans is { } spans)
        {
            foreach (var run in spans)
            {
                // An <a> when the run links, a <span> otherwise. Both are inline, so the paragraph
                // still wraps between WORDS — which is the whole reason a link has to live in a run
                // rather than in a Row of Texts.
                var runElement = new RealizedElement(run.Destination is { Length: > 0 } ? "a" : "span")
                {
                    InnerHtml = run.Content,
                    Style = new HtmlStyle
                    {
                        Color = run.Color is { } runColor ? TokenCss.Value(runColor) : null,
                        FontFamily = run.Mono ? TokenCss.MonoStack : null,
                        FontWeight = run.Weight is { } runWeight ? ((int)runWeight).ToString() : null,
                        FontStyle = run.Italic ? "italic" : null,
                        // SIZE only — no line-height. A run keeps the paragraph's line box, which
                        // is what makes it a run rather than a line of its own.
                        FontSize = run.StyleOverride is { } runStyle
                            ? TokenCss.Px(runStyle.ScaledSize(_context.TypeScale))
                            : null,
                    },
                };
                if (run.Destination is { Length: > 0 } destination)
                    runElement.RawAttributes = new Dictionary<string, string> { ["href"] = destination };
                element.Children.Add(runElement);
            }
        }

        // Gradient text: the background is painted through the glyphs. `color` stays as the
        // FALLBACK (a browser without background-clip:text renders readable solid text rather than
        // an invisible headline), and the fill-color override is what makes the clip visible.
        if (text.Gradient is { } gradient)
        {
            element.Style!.BackgroundImage = TokenCss.Gradient(gradient);
            element.Style.BackgroundClip = "text";
            element.Style.WebkitTextFillColor = "transparent";
        }

        // Single line → shaping-style ellipsis (spec A8).
        if (text.MaxLines == 1)
        {
            // MONO keeps `pre`: nowrap COLLAPSES runs of spaces, and in code the spaces ARE the
            // content — every indented line of the playground's editor started at column zero
            // while Photon (which draws literal glyph runs) indented it correctly. `pre` refuses
            // to wrap exactly like nowrap, so the ellipsis contract is untouched.
            element.Style!.WhiteSpace = mono ? "pre" : "nowrap";
            element.Style.Overflow = "hidden";
            element.Style.TextOverflow = "ellipsis";
            // BLOCK, or the other two do nothing. A Text lowers to a `span`, and `overflow` and
            // `text-overflow` are inert on a non-replaced INLINE box — so a squeezed single-line
            // Text painted its full width straight out of its parent instead of ellipsising inside
            // it: in a topbar that ran the placeholder over the ⌘K chip and off the screen. The
            // multi-line clamp below already sets a display for exactly this reason.
            //
            // Block rather than inline-block: block takes the width the parent allows, which is
            // what gives overflow something to clip against. Inline-block sizes to its content and
            // would spill again. Inside a flex row — where most single-line Texts live — the two
            // are identical, because a flex item is blockified either way.
            element.Style.Display = Display.Block;
        }
        // MULTI-LINE clamp: the paragraph occupies exactly N lines and ends in an ellipsis — what
        // keeps a grid of cards on one baseline when the copy is not the site's to control (a
        // registry description, a user's bio). CSS line-clamp; Photon fence: the shaper truncates
        // at the line the box allows.
        else if (text.MaxLines > 1)
        {
            element.Style!.Display = Display.WebkitBox;
            element.Style.WebkitBoxOrient = "vertical";
            element.Style.WebkitLineClamp = text.MaxLines.ToString();
            element.Style.Overflow = "hidden";
        }

        // System table override (e.g. Button labels) — inline styles beat the role class.
        if (text.StyleOverride is { } style)
        {
            element.Style!.FontSize = TokenCss.Px(style.Size);
            element.Style.LineHeight = TokenCss.Px(style.LineHeight);
            element.Style.FontWeight = ((int)style.Weight).ToString();
            element.Style.LetterSpacing = TokenCss.Px(style.Tracking);
        }

        return element;
    }
}
