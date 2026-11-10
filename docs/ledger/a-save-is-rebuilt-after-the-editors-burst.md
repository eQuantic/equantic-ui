- **2026-11-09 · A web save is rebuilt after the editor's burst, and none is dropped**: the server
  waited a fixed 400ms after a save before it rebuilt, measured as 44% of the second an edit has to
  reach the screen in, and dropped a save that came while a rebuild ran, so the browser stayed on the
  edit before it. The wait is a 50ms quiet window that each change of a save restarts, on one timer
  for the service's life, and a save during a rebuild runs one more rebuild when it ends, however many
  came. Measured on the dashboard sample, the wait part went from 436 to 475ms to 91 to 97ms, and a test
  counts two rebuilds for three saves where the old service ran one. The first of the web's
  edit-to-view slices ([#1274](https://github.com/eQuantic/equantic-ui/issues/1274),
  [#1262](https://github.com/eQuantic/equantic-ui/issues/1262)). Proposed through OpenSpec
  (`openspec/specs/hot-reload`).
