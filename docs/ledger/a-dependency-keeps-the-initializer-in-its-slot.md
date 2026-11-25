- **2026-11-23 · A component's dependency leaves no slot before its initializer**: a component whose
  constructor takes a dependency, built with an object initializer, lost the initializer in the browser, and an
  argument it omitted after the dependency arrived as `null`. `new Quark(_clock) { Size = 5 }` was
  `new Quark(null, 'calm', { size: 5 })`. The call drops the dependency's argument, as the twin's signature drops
  the parameter, but the defaults before the config were counted over the C# parameters, the dependency included.
  One rule, `LeavesOut`, now says which parameters the twin keeps, and both the drop and the defaults read it
  ([#1827](https://github.com/eQuantic/equantic-ui/issues/1827)). Proposed and archived through OpenSpec
  (`openspec/specs/transpiler-classes`).
