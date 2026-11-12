- **2026-11-11 · A Photon app's theme is chosen with UseTheme alone**: `PhotonOptions` held the theme,
  and the configuration binder's generator walked it through every record the theme's slots hold, so a
  slot with a `TypeStyle` member wrote code that did not compile in the Photon host. The theme leaves
  the bound options, `UseTheme` keeps it, `PhotonApplication.Theme` carries it to the shells, and the
  host no longer suppresses the binder's warnings
  ([#1398](https://github.com/eQuantic/equantic-ui/issues/1398)). Proposed through OpenSpec
  (`openspec/specs/design-system`).
