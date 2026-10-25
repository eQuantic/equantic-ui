# Proposal

Closes #1038, a Feature under #1035 (Any design system in C#, complete on the web and on native, with no CSS). This is its first slice: the document drawn from the theme. Loading the faces a theme names, from files the app declares in C#, is the second.

## Why

Every app on the SDK passes `SetBaseStyles` the same block. Measured on 2026-10-24 in five (Cura, falei.pt, eQuantic's site, eQuantic Family and eQuantic.Auth's screens), it holds:

- the body's background, colour and face, and its antialiasing;
- `--eq-font-family` or `--eq-font-mono` on `:root`;
- in some, `::selection`, the scrollbars and the placeholder's colour.

Two of them read `var(--eq-color-*)`, a contract nothing declares.

The SDK's own block (`HtmlShellOptions.BaseStyles`) sets `system-ui` and reads nothing from the theme, and `SetBaseStyles` replaces it, so each app restates `margin: 0` as well. The theme has a slot for the code face (`MonoFamily`) and none for the face everything else is set in, so the documented way to name it is a CSS variable an app writes on `:root`. The selection, the placeholder and the scrollbars are not the theme's either. On the SDK's own surfaces each is a constant written beside its component:

- the code editor's selection is `FocusRing` at 28%;
- a field's placeholder is `TextMuted`;
- a table's scrollbars are `TextMuted` at 55% over `SurfaceSubtle`.

Two measurements make it worse than a missing slot:

- **The cascade runs backwards.** The theme's stylesheet comes after the app's own CSS in the head, so on a tie the SDK's rule beats the app's.
- **An app with no `UseTheme` gets no stylesheet at all.** The table's scrollbars then read variables nothing defines, and draw nothing on the web.

## What Changes

**`IAppTheme` names the rest of the document.** It gains five slots:

- `TextFamily`, the face text with no face of its own is set in, beside `MonoFamily`;
- `Selection`, the wash behind selected text;
- `OnSelection`, the selected text's colour, or null to keep its own;
- `Placeholder`;
- `ScrollbarThumb` and `ScrollbarTrack`.

Each default is the value the SDK already draws with: `FocusRing` at 28%, `TextMuted`, `TextMuted` at 55% and `SurfaceSubtle`. A theme that sets none draws on Photon exactly as before.

**The web writes the document from the theme.** The theme's stylesheet now carries the document's rules:

- the body's margin, background, colour, face and antialiasing;
- the selection;
- the placeholder of every field;
- the scrollbars.

It is written for every app, Photon's theme when the app chose none, and it comes before the app's own CSS, so an app's rule wins a tie.

**`SetBaseStyles` and `BaseStyles` go.** `AddStyles(css)` adds the app's own CSS after the SDK's: the escape hatch for what a theme cannot say yet, appended, never a replacement for the SDK's rules.

**Photon and the components read the slots.**
- Photon sets text with no face of its own in `TextFamily`, through the merge the code face already goes through, renamed `WithThemeFace`.
- A field's placeholder, a table's scrollbars and the code editor's selection read the slots on both targets. The text a selection covers takes `OnSelection` when the theme names one, so a theme can draw its selection opaque without hiding the text under it.

**The files this touches hold one type each.** `IAppTheme.cs`, `UIExtensions.cs` and `TokenCss.cs` split, and their entries leave the one-type-per-file baseline. The theme's stylesheet generator is renamed from `PhotonCssGenerator`, a name from before it served any theme, to `ThemeStylesheet`. `HtmlShellOptions.BaseStyles` also centred the placeholder a page the server does not render shows until its module mounts. That rule moves to the theme's stylesheet, under the class `eq-loading` that `boot.ts` already wrote, so the shell and `boot.ts` now write one class.

**A page that opted out of server rendering is not an error.** The server told a page marked `DisableSsr` from one whose render broke by the placeholder's text, which both carry, so in production an app with a 500 page answered every `DisableSsr` page with it. `ServerRenderResult.Failed` says which, and only a render that broke is answered with the 500 page.

### For a developer using the SDK

An app deletes its `SetBaseStyles` call. Its body rules are the SDK's now, and the face, the selection, the placeholder and the scrollbars are said in the theme. Anything else moves to `AddStyles`.

The migration line is: `shell.SetBaseStyles(css)` → delete it, or `shell.AddStyles(css)` for what the theme cannot say.

On the web, an app that sets nothing new sees its selected text washed in the theme's focus colour instead of the browser's. Its page scrollbars stay the platform's own until the theme sets the scrollbar slots, so an app's own scrollbar CSS still applies.

Text is antialiased on macOS, as all five apps measured had asked for by hand.

### Parts reached

- **Primitives:** the theme's contract, and the type merge.
- **Web realizer:** the theme's stylesheet, and a table's scrollbars.
- **Server:** the head's order, the shell's options, the template, and when the 500 page answers.
- **Components:** the code editor's selection.
- **Photon:** a field's placeholder, a table's scrollbars, the face it sets text in.
- **Runtime:** the theme's twin, its defaults and the bridge.
- **Dashboard sample.**

The public surface moves in Primitives, Web, Server and Components. These go:
- `SetBaseStyles`, `BaseStyles`;
- `CodeBlock.SelectionAlpha`;
- `WithCodeFace`, renamed;
- `PhotonCssGenerator`, renamed.

The developer surface does not move: no MSBuild property and no configuration section changes.

## Capabilities

### New Capabilities

- `document-theme`: what the document takes from the theme on each target, the slots for the selection, the placeholder and the scrollbars, and where an app's own CSS stands against the SDK's.

### Modified Capabilities

- `typography`: text with no face of its own is set in the theme's text face.
- `page-routes`: the 500 page answers only a render that broke, never a page that opted out of server rendering.
