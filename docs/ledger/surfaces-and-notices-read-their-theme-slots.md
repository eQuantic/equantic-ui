- **2026-11-11 · The surfaces, menus, notices, progress and cards read their theme slots**: E6c, the
  third slice of the theme slots. `IAppTheme` gains `Modal` (Dialog, BottomSheet, Drawer), `Menu`
  (Popover, Menu, Tooltip), `Notice` (Toast, Banner, CookieConsent), `Progress` (ProgressBar,
  PullToRefresh, Skeleton) and `Card`, each a record with every member optional that holds what its
  components wrote as constants. `PhotonTheme` fills them with the sizes its components always had and
  `MaterialTheme` with Material 3's specifications, and they cross to the browser with the theme. A
  colour one of the theme's roles already gives stays null in both themes, as E6b's rule has it: the
  tooltip, the toast, the progress track, the skeleton, the filled card and the sheet's handle read
  the role, so a theme that recolours `TextPrimary` or `SurfaceSubtle` recolours them too. The
  surfaces' slot is `Modal` rather than `Surface`, because the theme's JSON already writes the surface
  colours under `surfaces`, and the browser would have read one over the other
  ([#1243](https://github.com/eQuantic/equantic-ui/issues/1243)). Proposed through OpenSpec
  (`openspec/specs/theme-slots`).
