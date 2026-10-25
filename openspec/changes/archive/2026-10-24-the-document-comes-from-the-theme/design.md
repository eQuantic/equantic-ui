## Context

The theme already decides most of what a page looks like. The parts of the document around the components still come from somewhere else:

- the body, written in CSS by each app;
- the face text is set in, named through a CSS variable;
- the selection, the placeholder and the scrollbars, which are constants beside the components that draw them.

`docs/FLUTTER-PARITY.md` records the stance this change keeps: one `IAppTheme` for every target (the `ThemeData` vs `CupertinoThemeData` row).

On the web the head holds, in order:

1. the framework's invariants;
2. the app's `BaseStyles`;
3. the app's head tags;
4. the theme's stylesheet, appended last for each request.

`TokenCss` documents that "an app's own `:root` rule still wins". On a tie, the order says the opposite.

## How Flutter solves it

`ThemeData` carries the document's parts as members and sub-themes of the one theme:

- `fontFamily` for the face of every text with none of its own;
- `scaffoldBackgroundColor` for the surface;
- `TextSelectionThemeData.selectionColor`, by default the primary colour at 40%;
- `InputDecorationTheme.hintStyle` for the placeholder;
- `ScrollbarThemeData` for the thumb and the track.

A widget reads them from `Theme.of(context)`. Nothing in Flutter writes a stylesheet, because nothing in Flutter has a document. Its fonts are declared in `pubspec.yaml` and bundled. Declaring and loading faces is this feature's second slice.

## Decisions

**The slots are colour roles of the theme, beside `FocusRing`, `LinkColor` and `Scrim`.**
- `Selection`, `Placeholder`, `ScrollbarThumb` and `ScrollbarTrack` are `ColorToken` members with defaults. They cross to the browser as every colour role does: `ThemeBridge` serializes each `ColorToken` member of `IAppTheme`, so the bridge gains no code for them.
- `OnSelection` is nullable, because "keep the text's own colour" is the default and no colour says it. It crosses explicitly, as `MonoFamily` does.
- Flutter groups the selection and the scrollbars into sub-themes. Here they stay flat, the shape the colour roles already have. The sub-theme records belong to per-component slots (#938), which are not this.

**Each default is the value the SDK already draws with.** That is `FocusRing` at 28% (the code editor's selection), `TextMuted` (a field's placeholder), and `TextMuted` at 55% over `SurfaceSubtle` (a table's scrollbars). So a theme that sets none draws on Photon exactly as before, and the goldens hold. The web's document takes the same values, which is the visible change, and the right one: a page's own selection and scrollbars now match the SDK's.

**The document's rules live in the theme's stylesheet, and the stylesheet comes before the app's CSS.**
- The order in the head becomes: the invariants, the theme's stylesheet, the app's own styles, then the app's head tags.
- The rules are ordinary selectors (`body`, `::selection`, `::placeholder`, `:root`), so the app's own rule wins every tie by coming later. That is the rule the template already states for the invariants: before app styles, so an app can deliberately override.
- A cascade layer would have done the same with less clarity. An app's rule would win however specific the SDK's was, and nothing in the page would say why.

**There is always a stylesheet.**
- When the app chose no theme, the server writes Photon's, the theme the browser runtime already falls back to.
- The variables the lowered markup reads (the table's scrollbars name them directly) are then defined on every page, which they were not.
- `window.__EQ_THEME__` stays as it is: written only for a chosen theme, since the runtime carries Photon's.

**`AddStyles` replaces `SetBaseStyles`.**
- "Set" replaced the SDK's block, and once the SDK's rules come from the theme there is nothing for an app to replace.
- What an app still needs is a place for CSS the theme cannot say yet: a list it appends to, written after the SDK's rules.
- It is an escape hatch with its reason in its documentation, never the way to style a document.

**The text face resolves where the code face does.**
- `EffectiveTypeStyle` is the one merge of a role, a node and the theme for Photon and for the email. Its `WithCodeFace` becomes `WithThemeFace`: a monospaced style with no family takes `MonoFamily`, and any other style with no family takes `TextFamily`.
- A family the role or the call site named is left alone, the rule the code face already follows.
- The web needs no part of it: the document's face is inherited, and the role classes name a face only when the role has one.

**The files this change edits hold one type each.** The one-type-per-file baseline may only shrink, and these three entries say "not looked at yet":
- `IAppTheme.cs` splits off `Variant`, `ShapeScale` and `SizeVariant`;
- `UIExtensions.cs` splits off `UIOptions`, `HtmlShellOptions`, `ThemeCookie` and `ThemeCookieOptions`;
- `TokenCss.cs` splits off the stylesheet generator.

The generator is renamed `ThemeStylesheet`. Its name from before it served any theme read as a native class on a web path. Citations that name a line of these files in the documentation move with them.

## Risks and trade-offs

- **The web's selection and scrollbars change for every app** that sets nothing. That is the intent, and it is visible. A theme that wants the browser's own scrollbars has no slot that says "the platform's", the price of defaults that are colours. The same holds for a page that wants the system's selection: `AddStyles` is the way out until a design asks for a slot.
- **`scrollbar-color` paints the scrollbars on Chromium and Firefox.** Safari keeps its own, and `color-scheme` already gives them the theme's mode.
- **The text face is named, not loaded,** until the second slice. A face the machine does not have falls back on the web as CSS does, and Photon reports it unresolved, as it does for a role's face today.
- **#1029 is open on the same files.** It adds a per-component slot through the bridge, the twins and `IAppTheme`, so one of the two merges the other's lines.
