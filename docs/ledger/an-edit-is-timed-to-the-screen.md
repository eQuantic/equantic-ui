- **2026-11-09 · An edit is timed from the save to the screen, on the web and on the desktop**: the
  roadmap's bar says an edit reaches the screen in under a second, and nothing had ever measured it.
  `scripts/measure-edit-to-view.cs` runs a sample under `dotnet watch`, changes a text literal of a
  component on disk, and stops the clock when a browser or the window draws the new text, each part of
  the path timed on its own clock: eqc reports its phases, and the Photon host reports the frame that
  draws an edit. CI's samples job runs it last. Its first clean run measured a warm median of 329ms on
  the desktop, held to the bar of a second, and 22.9s on the web, held to a ceiling of 45s that only
  comes down as the slices of #900 make each part faster (#1274 to #1281). Measuring it found the
  desktop sample restarted on every edit, since publishing trimmed turned .NET's startup hook off for
  every build: the native SDK now keeps it on for the Debug build of a desktop head that is not a
  publish ([#900](https://github.com/eQuantic/equantic-ui/issues/900),
  [#1258](https://github.com/eQuantic/equantic-ui/issues/1258)). Proposed through OpenSpec
  (`openspec/specs/hot-reload`).
