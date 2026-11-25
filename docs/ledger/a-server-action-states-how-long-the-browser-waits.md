- **2026-11-24 · A Server Action states how long the browser waits for it**: the browser abandoned every
  action at 30 seconds, since eqc wrote each stub as `invoke('<id>', [args])` though the runtime has always
  taken a `timeoutMs`, and a real site's action that took 99.6 s left its page waiting for good while the
  server finished the work. `[ServerAction(TimeoutSeconds = 120)]` is read as a constant and handed to the
  stub as `{ timeoutMs: 120000 }`; none, or 0, keeps the 30 seconds and the stub's shape, and a value below 0
  or above 600 is refused at its line (EQ2017). Proved in `samples/DefaultUIDashboard`, where an action made
  to take 35 s and marked 60 answered in a browser
  ([#1881](https://github.com/eQuantic/equantic-ui/issues/1881)). Proposed and archived through OpenSpec
  (`openspec/specs/server-actions`).
