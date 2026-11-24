- **2026-11-24 · Buttons, selection controls and progress take a style of their own**: `Button`, `FormSubmit`,
  `IconButton`, `Chip`, `Checkbox`, `RadioGroup`, `Switch`, `Slider`, `ProgressBar`, `PullToRefresh` and `Skeleton`
  take their family's slot record as `style:`, laid over the theme's slot member by member, on the web, in the
  browser's twin and on Photon, and a `RadioGroup` draws its options with an `optionBuilder` while each option's
  name stays its accessible name. Eleven `style` entries leave the catalog's guard
  ([#1606](https://github.com/eQuantic/equantic-ui/issues/1606), the style and builders of
  [#1547](https://github.com/eQuantic/equantic-ui/issues/1547)). Proposed and archived through OpenSpec
  (`openspec/specs/catalog-customization`).
