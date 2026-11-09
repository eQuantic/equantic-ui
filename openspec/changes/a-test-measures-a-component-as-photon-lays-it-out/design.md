# Design

## How Flutter answers it

`flutter_test`'s `WidgetTester` measures with the same calls that drive: `tester.getRect(finder)`,
`getSize`, `getTopLeft`, over the render tree the frame laid out, with no window. Its text is the
detail that makes a measurement portable: a test renders every glyph in a test font (`Ahem` first, and
`FlutterTest` now) whose glyphs are boxes of the font's size, so a paragraph wraps at the same word on
every machine, and the goldens draw the boxes. docs/FLUTTER-PARITY.md has no row for the test harness;
T1 already named `ComponentTester` after `WidgetTester`.

The engine here has both halves already. `ApproximateTextMeasurer` is the deterministic stand-in every
native golden measures with (fixed advances per character class, a greedy wrap), and a frame with no
text rasterizer draws each line as a placeholder bar. That is Flutter's test font, and the package uses
it for the same reason.

## Decisions

### A package of its own, on the native track

The native half cannot live in `eQuantic.UI.Testing`'s assembly: a package's dependencies are every
consumer's, so a web-only app's test project would restore the Photon engine to test a page. It is
`eQuantic.UI.Native.Testing`, and the name does the rest. `Directory.Build.targets` derives the
`experimental` class from the `eQuantic.UI.Native.` prefix and writes `[Experimental("EQX0001")]` into
the assembly, so a test project that measures says it means to, as a project that uses the engine does.
Inside the package the engine's types raise nothing: the compiler does not report an experimental
symbol used from an experimental context.

The web half staying clean is pinned rather than promised: `AssemblyLayeringTests` holds
`eQuantic.UI.Testing` to the web realizer alone, so an edge to the native track fails a test.

### Through `PhotonHost`, the shells' frame driver

`PhotonRealizer.Realize` lays a tree out once, but a frame is more than that: the live owner across
frames, the density that rebuilds every control, the overlay layers shown in their own pass, the
semantics a bridge reads. `PhotonHost` holds all of it and runs without a window (the native suites
construct it directly). The tester is a host of it, so what a test measures is what a shell lays out,
and `Pump()` draws what a `SetState` marked, as T1's does. No fifth kind of owner appears: the Photon
host is the one docs/ELEMENT-TREE-PLAN.md 3.6 already lists.

### A snapshot of the layout, not the engine's nodes

`LayoutNode` is the engine's render tree: its setters are internal to the engine, a host that recycles
frames hands its instances to the next frame, and it carries host state (the position, presence, drag
offset). The tester runs the host as every production shell does, recycling frames, so the nodes
of one frame are written over by the frame after the next, and a test holding one would read
whatever that frame wrote into it. `MeasuredNode` copies
what the frame measured: the node laid out, its box in dp (absolute, as the layout computes it), the
lines its text measured, and its children. The text keeps the engine's `TextMeasurement`, which is
already immutable: a second type would hold a copy.

### Found by the name a screen reader announces

`Find(name)` reads Photon's own semantics tree, `PhotonHost.Semantics()`, and maps the announced node
to its layout by the id both carry, the position's. It is T1's rule on the other target: a test names a
control once, as a person who cannot see the screen finds it, and the same name finds it on the web and
on Photon. A name announced by none, or by two, fails with what is announced.

### Pixels through the reference backend

`ReadPixelsSrgb()` paints the last frame's display list with `ReferenceBackend`, the CPU backend every
GPU backend is held to. It is what lets the package be proven against a golden rather than against
itself, and it costs nothing until a test asks.

### The culture around every frame

As in T1, a component formats with the tester's culture and reads its labels in it, as both
`CurrentCulture` and `CurrentUICulture`, restored when the call returns. The scope is the package's
own: no shipped assembly grants its internals to another, and the scope is twenty lines.

## Divergences from the plan

None. The host is `PhotonHost`; docs/ELEMENT-TREE-PLAN.md needs no change.
