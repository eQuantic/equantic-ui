# Proposal

Closes #340, a Feature under #1035 (any design system in C#, complete on the web and on native).

## Why

iOS and macOS have Increase Contrast, Android 14 has contrast levels, the web has
`prefers-contrast: more`, and Material 3 generates standard, medium and high contrast schemes.
Photon resolved every colour by `ThemeMode` alone, so a person who asked their system for more
contrast got the palette everyone else got. The handoff specifies a second resolution axis
(Foundations §01, "Contrast levels"), and its "More" palette waited in `docs/design/proposals.json`.

## What Changes

- `ContrastLevel { Standard, More }` in Primitives, and `ColorToken.Resolve(ThemeMode, ContrastLevel)`.
  A token holds a More pair beside its Standard one (`WithMore`), and a token without one resolves
  to Standard, so a theme that never heard of contrast levels paints the same at both.
- PhotonTheme ships the handoff's More palette: eighteen tokens, one APCA level up, never below the
  WCAG 2 targets. The values move from `proposals.json` into `tokens.json` under `color.more`, and
  `status.json` and the Foundations page call the item shipped.
- On the web, the theme's stylesheet declares its colour variables again under
  `@media (prefers-contrast: more)`. Every atomic class reads a theme colour through its variable,
  so the page steps up with no class of its own. A variant's derived hover becomes a variable of its
  own on both sides, the C# atomizer's map and its TypeScript twin, so it steps up with its fill.
- On Photon, `PhotonHost.Contrast` carries the level beside the mode to every token the frame
  paints. macOS reads Increase Contrast from `NSWorkspace`, iOS from its trait collection (following
  changes), Android 14 from `UiModeManager` (with its change listener). Windows high-contrast themes
  replace the whole palette and are a separate request, as the handoff says, so Windows paints
  Standard.
- An app writes nothing: the system's setting is the input, as the mode's is.

**Breaks** (preview): `ColorToken.Resolve(ThemeMode)` becomes `Resolve(ThemeMode, ContrastLevel)`,
and the experimental `PhotonRealizer.Realize` takes `contrast` after `mode`. Migration: pass
`ContrastLevel.Standard` for the colour it answered before. The developer surface does not move.

## Capabilities

### New Capabilities

(none)

### Modified Capabilities

- `design-system`: colours resolve at a contrast level beside the mode, and the palette clears its
  floors at each level.

## Impact

Primitives (`ContrastLevel`, `ColorToken`, `PhotonTheme`), the web realizer (`ThemeStylesheet`,
`TokenCss`, `ThemeVarMap`) and the runtime's atomizer, the Photon realizer and host, the macOS, iOS
and Android shells, the email realizer, `docs/design/` (tokens, proposals, status, Foundations), and
the wiki's DesignSystem page in five languages and Upgrading in two.
