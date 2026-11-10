- **2026-11-10 · A member named Component is that member**: a Bug on 1.0's critical path (#1127),
  [#1299](https://github.com/eQuantic/equantic-ui/issues/1299). eqc read every bare `Component` its scope did
  not bind as `this._component`, a field of the State classes the runtime no longer has, so the element
  engine's property stayed null in the browser with no error, and an app's member of that name held two
  values. The name is read by its symbol now, as every other name is. Measured on main, the new
  conformance case fails there. Proposed and archived through OpenSpec (`transpiler-names`).
