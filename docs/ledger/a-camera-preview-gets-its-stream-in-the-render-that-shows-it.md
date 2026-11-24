- **2026-11-23 · A camera preview gets its stream in the render that shows it**: the browser attached the camera
  streams at the end of a pass, before the render manager wrote the pass's tree, so a preview a render showed played
  nothing until a later pass came. What the end of a pass does against the written document now runs at one moment,
  the microtask after the write, the focus traps and the camera streams alike
  ([#1830](https://github.com/eQuantic/equantic-ui/issues/1830)). Proposed and archived through OpenSpec
  (`openspec/specs/device-camera`).
