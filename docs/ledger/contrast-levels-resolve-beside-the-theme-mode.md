- **2026-11-10 · Contrast levels resolve beside the theme mode**: a person who turns their system's
  contrast up gets PhotonTheme's More palette, one APCA level up and never below the WCAG 2 targets,
  with nothing written by the app. `ColorToken.Resolve` takes a `ContrastLevel` beside the mode, the
  web declares the theme's colour variables again under `prefers-contrast: more`, and Photon reads
  Increase Contrast on macOS and iOS and the contrast level on Android 14. The handoff's eighteen
  More values moved into `tokens.json`, pinned both ways, and the token and APCA gates run once per
  level ([#340](https://github.com/eQuantic/equantic-ui/issues/340)). Proposed and archived through
  OpenSpec (`openspec/specs/design-system`).
