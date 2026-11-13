- **2026-11-11 · The element engine runs in the browser**: the last slice of E3a. The engine written once
  in C# leaves `[ServerOnly]`, and its modules are committed in the runtime beside the code editor's and
  the spreadsheet's, pinned byte for byte and accepted by the runtime's `tsc`. Twelve cases the C#
  engine is held to run again on the transpiled engine with the same components and steps, and the two
  traces are equal: retention by type and key, keyed and unkeyed moves, a type change, the build order,
  two siblings with one key, one instance at two positions, InFlow, and a frame value only its readers
  rebuild for. The inherited value's position crosses too, and the runtime's context reads a position's
  frame from its owner, as C#'s does since [#1412](https://github.com/eQuantic/equantic-ui/pull/1412).
  They found two gaps, both
  closed: the browser's `UiComponent` had no `AdoptConfig` default, and a library class extending a
  sibling read its base off the runtime's aggregator before the barrel had loaded. The stateful
  component's `Place` and `Leave` became `EnterPosition` and `LeavePosition`, which eqc refuses as an
  app member's name. Nothing in the browser drives the engine yet; E3b does
  ([#1301](https://github.com/eQuantic/equantic-ui/issues/1301), closing
  [#1127](https://github.com/eQuantic/equantic-ui/issues/1127)). Proposed and archived through
  OpenSpec, adding a requirement to `element-tree`.
