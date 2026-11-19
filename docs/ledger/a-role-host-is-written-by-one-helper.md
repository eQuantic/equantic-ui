- **2026-11-19 · A role-bearing wrapper's host is written by one helper on each producer**: `Progress`, `Adjustable`,
  `LiveRegion` and `Group` each wrote their host's width contract by hand, `LowerGroup` a near-copy of
  `LowerLiveRegion` and `lowerGroup` of `lowerLiveRegion`, and that contract had already diverged between the two
  producers once. Each producer now writes a role host through one helper (the role's attributes in, the host out),
  the wrappers call it, and `RoleBearingHostBoundsTests` and `role-host-bounds.spec.ts` hold every row with no pin
  moving ([#1517](https://github.com/eQuantic/equantic-ui/issues/1517)). Proposed and archived through OpenSpec
  (`openspec/changes/archive/`).
