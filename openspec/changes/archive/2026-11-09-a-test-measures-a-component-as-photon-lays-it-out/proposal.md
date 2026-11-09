# Proposal

Closes #1246, the second slice (T2) of Feature #294, under the 1.0 epic #898.

## Why

T1 lets an app's test read what the web lowers and what a screen reader announces. It cannot ask the
other question a write-once component answers twice: what Photon lays out. The box a card takes at a
phone's width, whether a name fits on one line or wraps to two, where the button lands below it: today
only the repository's own suites can ask, through `PhotonRealizer.Realize` and a hand-built display
list, and an app author would have to learn the engine's types to repeat it.

## What Changes

- **A package, `eQuantic.UI.Native.Testing`**, the native half of T1's package. An app's test project
  references it beside `eQuantic.UI.Testing` when the app also ships through Photon:

  ```csharp
  using var tester = new PhotonTester(width: 360, height: 640);   // a phone's width
  tester.Pump(new ProfileCard("Ana Lima", "Principal Engineer, Platform Reliability and Developer Experience"));
  var role = tester.Find("Principal Engineer, Platform Reliability and Developer Experience");
  Assert.Equal(2, role.Text!.Lines.Count);                      // one line at 1280 dp
  Assert.True(role.Bounds.Right <= tester.Root.Bounds.Right);
  ```

  `PhotonTester` takes the theme, the culture, the viewport's width and height in dp, the density, the
  theme mode and the type scale, each optional. `Pump(root)` draws the first frame through a headless
  `PhotonHost`, the frame driver the shells use, and `Pump()` the next. `Root` is the page's layout and
  `Layers` each shown overlay's, as `MeasuredNode`s: the node laid out, its box in dp, the lines its text
  measured, and its children. `Find(name)` and `Find(role, name)` answer the node a screen reader
  announces by that name, the way T1 finds a control. `ReadPixelsSrgb()` paints the frame through the
  CPU reference backend.
- **No window and no GPU, and the same answer on every OS.** Text is measured by the engine's
  deterministic measurer and drawn as its placeholder bars, as every native golden is, so a box a test
  pins on a Mac is the box CI measures on Linux.
- **The package is experimental, as the native track is.** Its name puts it on the track, so it carries
  `[Experimental("EQX0001")]` like every `eQuantic.UI.Native.*` assembly, and a test project that uses
  it says it means to. A project that references only `eQuantic.UI.Testing` never sees the native track:
  the web half references nothing of it, and the layering pin now says so.

## Capabilities

### Modified Capabilities

- `component-testing`: a test measures a component as Photon lays it out, with no window, and finds a
  node by the name it announces.

## Impact

- New project and package `eQuantic.UI.Native.Testing` (`experimental` surface, by its name),
  referencing `eQuantic.UI.Native.Components` and `eQuantic.UI.Native.Engine.Reference`; its API
  declared in `PublicAPI.Unshipped.txt` with the `[EQX0001]` prefix.
- `AssemblyLayeringTests` gains the two testing packages: `eQuantic.UI.Testing` references the web
  realizer alone, and `eQuantic.UI.Native.Testing` the Photon host and the reference backend.
  `docs/ARCHITECTURE-AUDIT.md` says why.
- eqc, the runtime, the web realizer, the Photon engine and shells, the SDKs' targets and the templates:
  no change. Nothing public changes outside the new package, and the developer surface does not move.
- Docs: the wiki's `Testing` page gains one example, in its five languages, and a ledger entry.
- Nothing is removed, so no app needs a migration line.
