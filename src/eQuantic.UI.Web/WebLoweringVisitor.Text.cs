using System.Linq;

using eQuantic.UI.Primitives;

namespace eQuantic.UI.Web;

/// <summary>
/// Text and the editable surfaces. <c>Text</c> is the single largest arm here — the type ramp, the
/// runs, the clamp and the gradient ink all resolve in it — and the two surfaces are the pair whose
/// SSR shape was settled one at a time: <c>SheetSurface</c> first, and <c>CodeSurface</c> once the
/// client could draw again what the server could not measure.
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

    /// <summary>
    /// An editable code surface, as the client builds it (lowering.ts, <c>lowerCodeSurface</c>):
    /// the surface, its child in a stacking context of its own, the carets the MODEL answers, and
    /// the input the keyboard types through, at the primary caret. A caret written here is never
    /// seen early: the generated sheet hides it until the surface holds the keyboard
    /// (<c>:focus-within</c>).
    /// <para>
    /// Its geometry is built on widths this side cannot measure, because the component that owns
    /// the model measured its code through the context, and <see cref="FontlessMeasurer"/> answered
    /// 0. That component is marked, and the client draws its subtree again at hydration: what this
    /// arm is for is the CODE, in the server's HTML for a crawler and for a reader without
    /// JavaScript, where there was a hole.
    /// </para>
    /// <para>
    /// No events: the client attaches them. The input's own (<c>beforeinput</c>, the composition
    /// events, the clipboard's) have no property on <c>HtmlElement</c> to declare them by, which is
    /// why the component parity fixture cannot hold this surface yet.
    /// </para>
    /// </summary>
    private HtmlElement LowerCodeSurface(CodeSurface surface)
    {
        var element = new RealizedElement("div")
        {
            ClassName = "eq-code-surface",
            Style = new HtmlStyle
            {
                PointerEvents = "auto",
                Position = Position.Relative,
                Outline = "none",
                // Token runs carry REAL spaces between words, which HTML would collapse.
                WhiteSpace = "pre",
                // A drag extends the MODEL's selection; the browser's own sweep would paint a
                // second one over the band the component draws.
                UserSelect = "none",
                Cursor = "text",
            },
        };

        // The child lowers with no inherited axis, as the client lowers it, and keeps its layers
        // (the code's mark layer among them) in a stacking context of its own, so a layer it raised
        // stays under the caret written after it.
        if (Lower(surface.Child, null) is { } child)
        {
            if (child is RealizedElement realized)
            {
                realized.Style ??= new HtmlStyle();
                realized.Style.Isolation = "isolate";
            }
            element.Children.Add(child);
        }

        var ink = TokenCss.Value(surface.CaretColor ?? _context.Theme.TextPrimary);
        var carets = surface.Model.Carets;
        foreach (var caret in carets)
        {
            element.Children.Add(new RealizedElement("div")
            {
                ClassName = "eq-code-caret",
                RawAttributes = new Dictionary<string, string>
                {
                    ["style"] = $"position:absolute;left:{TokenCss.Px(caret.X)};top:{TokenCss.Px(caret.Y)};"
                        + $"width:{TokenCss.Px(caret.Width)};height:{TokenCss.Px(caret.Height)};"
                        + $"background-color:{ink};pointer-events:none;",
                },
            });
        }

        // THE INPUT, at the primary caret, so an input method's window opens where the text lands.
        var input = new Dictionary<string, string>
        {
            ["style"] = carets.Count > 0
                ? $"left:{TokenCss.Px(carets[0].X)};top:{TokenCss.Px(carets[0].Y)};height:{TokenCss.Px(carets[0].Height)};"
                : "left:0;top:0;",
            ["autocomplete"] = "off",
            ["autocorrect"] = "off",
            ["autocapitalize"] = "off",
            ["spellcheck"] = "false",
            ["aria-multiline"] = "true",
        };
        if (surface.Label is { Length: > 0 } label) input["aria-label"] = label;
        if (surface.Autofocus) input["autofocus"] = "";

        // What the surface OFFERS at its caret (TS twin: lowerCodeSurface): over the code and the
        // caret, at its origin in the surface's coordinates, as the input's listbox. The rows are
        // options, numbered as LowerAnchored numbers a listbox's rows, and the input names the list and
        // points at the one the keyboard is on. An open list never comes from the server in practice
        // (it opens as someone types), so its id hashes its text, as LowerAnchored's does, and need not
        // match the client's spelling.
        RealizedElement? list = null;
        if (surface.Options is { } options && Lower(options, null) is { } offered)
        {
            var listId = $"eq-options-{StyleAtomizer.Hash(TextContentOf(options))}";
            list = new RealizedElement("div")
            {
                Id = listId,
                Role = "listbox",
                RawAttributes = new Dictionary<string, string>
                {
                    ["style"] = $"position:absolute;left:{TokenCss.Px(surface.OptionsOrigin.X)};"
                        + $"top:{TokenCss.Px(surface.OptionsOrigin.Y)};",
                },
            };
            list.Children.Add(offered);
            var next = 0;
            NumberItemRows(list, listId, ref next);
            // No aria-expanded: ARIA allows it on a combobox and not on the textbox a textarea is,
            // and the list it controls and the option it points at already say that one is showing.
            input["aria-autocomplete"] = "list";
            input["aria-controls"] = listId;
            if (surface.HighlightedOption >= 0) input["aria-activedescendant"] = $"{listId}-{surface.HighlightedOption}";
        }

        element.Children.Add(new RealizedElement("textarea")
        {
            ClassName = "eq-code-input",
            RawAttributes = input,
        });
        if (list is not null) element.Children.Add(list);
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
                // A BLOCK, wherever it sits: a Text is never inline in the vocabulary (inline runs
                // live in Spans). As an inline span inside a block parent (a Box, a Flexible, a
                // Link, a Pressable) it sat on the parent's line box, whose strut is the body font
                // at line-height normal, so a 10/15 label in a padded pill measured 26.5px where its
                // parts add up to 21 (#495). A flex or grid item is a block either way. As a block
                // it takes the width its parent gives, as Flutter's Text does under a tight width:
                // its alignment and its gradient read across that width. The multi-line clamp
                // below replaces it with the box the clamp needs.
                Display = Display.Block,
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
                if (run.Destination is { Length: > 0 } written)
                {
                    // A run that links is a link: the language rides its href and an app-internal one
                    // warms on hover, as a Link's does. It took the destination as written, so on
                    // /pt-BR/login a run to /terms led to the English page (#505).
                    var destination = RenderContext.ResolveDestination(written);
                    runElement.RawAttributes = new Dictionary<string, string> { ["href"] = destination };
                    if (WarmsOnHover(destination)) runElement.RawAttributes["data-prefetch"] = "";
                }
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
            // The other two need the BLOCK every Text is: `overflow` and `text-overflow` are inert
            // on a non-replaced inline box, so a squeezed single-line Text painted its full width
            // out of its parent instead of ellipsising inside it, and inline-block would size to
            // its content and spill again.
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
            // A size that follows the window is a `clamp()` and its line box a ratio (#652).
            element.Style!.FontSize = TokenCss.FontSize(style);
            element.Style.LineHeight = TokenCss.LineHeight(style);
            element.Style.FontWeight = ((int)style.Weight).ToString();
            element.Style.LetterSpacing = TokenCss.LetterSpacing(style);
        }

        return element;
    }
}
