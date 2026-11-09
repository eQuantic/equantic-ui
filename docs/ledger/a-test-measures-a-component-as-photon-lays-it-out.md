- **2026-11-09 · A test measures a component as Photon lays it out**: a new package,
  `eQuantic.UI.Native.Testing`, the native half of `eQuantic.UI.Testing`. Its `PhotonTester` drives
  a `PhotonHost` with no window and no GPU, as every shell drives one, frames recycled as a shell's
  are, and answers each node's box and its text's lines (`Root`, `Layers`), copied from the frame
  that measured them so a measurement a test holds stays true. `Find` reads Photon's own semantics
  tree by the name a screen reader announces, the name T1 finds a control by on the web. Text is
  measured by the engine's deterministic measurer and drawn as its placeholder bars, Flutter's test
  font in another form, so a box a test pins on a Mac is the one CI measures on Linux; and
  `ReadPixelsSrgb` paints a frame through the reference backend, which is how the package is held to
  the goldens: the wave-2 gallery pumped through it matches `wave2-gallery-light.png`. The package is
  on the native track by its name, so it carries EQX0001 as the track does, and the layering pin now
  holds the web half to the web realizer alone, so a web-only app's tests never meet the native track
  ([#1246](https://github.com/eQuantic/equantic-ui/issues/1246)).
