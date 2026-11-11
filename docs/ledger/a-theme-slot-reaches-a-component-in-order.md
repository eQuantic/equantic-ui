- **2026-11-10 · A theme slot reaches a component in order**: what made a control look like itself
  was written in the component or in `Sizing`, which no theme reached. `IAppTheme` gains its first
  two component slots, `Controls` (the ladder) and `Button`, resolved in the plan's order (the
  family's slot, the theme's `Controls`, then the default, at the context's density), and
  `theme.With(…)` builds a theme from another with one slot replaced and every other member forwarded.
  Every member of the theme now crosses to the browser, which found the code palette that never did
  (#936). `PhotonTheme` fills its slots from the ladder itself, so every page renders as before
  ([#1241](https://github.com/eQuantic/equantic-ui/issues/1241)). Proposed and archived through
  OpenSpec (`openspec/specs/theme-slots`).
