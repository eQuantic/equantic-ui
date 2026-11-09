# Proposal

Closes #1240, a Feature under #898 (1.0: the web SDK stable, with the native track experimental).
Asked by Edgar on reading the site's `AddHeadTag("""<link rel="icon" href="/favicon.ico" sizes="any">""")`:
is there a typed way to declare a favicon?

## Why

An app states its icon once (`IAppIcon` in `Assets/AppIcon.cs`, or `Assets/AppIcon.png`) and the
build writes the web set the server links. Neither app that ships today used it: the site wrote the
link by hand over its own `favicon.ico`, and myartwork3d kept a table of `(rel, href, type, sizes)`
over files a designer exported. Measured why, on the dashboard sample:

1. The icon could not be written like a file of the app. eqicon compiled `AppIcon.cs` alone, so
   `Box(...)` named the vocabulary's type instead of the factory (CS1955) and `Vectors`, the catalog
   of the app's SVGs, did not exist (CS0103): a brand that exists as an SVG had to be redrawn as
   shapes.
2. Every vector came out a placeholder. The engine draws a `Drawing` or an `Icon` through a platform
   rasterizer, eqicon handed it none, and none exists on Linux, where every container image builds.
   An icon drawing the app's own SVG rendered as a flat grey square.
3. One artwork where a brand has two. The launcher's icon fills its square, opaque; the tab's is
   another cut, transparent, with wider strokes.
4. No `/favicon.ico`. Every browser and every crawler asks for it at the root, and an app without a
   file there answered 404 to every one of them.

A typed `AddFavicon(rel, type, sizes)` was considered and refused: those are HTML's own words with
enums around them, and the developer would still learn which sizes a browser wants. The SDK knows
them, so the app states its artwork and the SDK derives every link.

## What Changes

- `IAppIcon.Build(ComponentContext context, AppIconUse use)`: one method, told where it is drawn,
  `AppIconUse.Launcher` or `AppIconUse.Small`. An app with one artwork reads nothing of `use`. The
  one-argument `Build` goes (preview).
- eqicon compiles the icon with the app's `Using` items, which both SDKs hand over, keeping the ones
  the vocabulary resolves, and with the app's vector catalog on the web.
- `PortableIconRasterizer`, in plain C#, draws every vector of an icon on every machine, so the icon
  is the same bytes on a laptop and in a container. `PhotonHost.Backdrop` lets the small cut be drawn
  on transparent rather than on the theme's background.
- The web set writes `favicon.ico` (16, 32 and 48, from the small cut with its transparency) in place
  of `favicon-32.png`; the server links it and answers `/favicon.ico` at the root, an app's own
  favicon first, a referenced project's static web asset included, and 404 with neither. The icon links are cached per web
  root rather than per process.
- The downscale weighs colour by alpha, so a transparent cut keeps no dark rim; an opaque icon's
  result is unchanged.
- The dashboard sample declares an icon with both cuts, from two SVGs under its `Assets`.

## Impact

- `eQuantic.UI.Primitives`: `IAppIcon.Build` widens, `AppIconUse` is new (a break: an app's icon
  takes the second parameter)
- `eQuantic.UI.Native.Build` (eqicon), `eQuantic.UI.Native.Framework`, `eQuantic.UI.Native.Components`
- `eQuantic.UI.Server`: `/favicon.ico` and the head's icon link
- Both SDKs' targets, the dashboard sample, `openspec/specs/app-icon` (a new spec)
- The wiki's Assets, BuildFlow and Architecture pages, and an entry under `docs/ledger/`
