- **2026-11-15 · Every accessor and indexer reaches its twin, and an accessor is never a server action**: a
  default interface event was refused (EQ1008) where a default property and method crossed
  ([#1511](https://github.com/eQuantic/equantic-ui/issues/1511)), the server registered an accessor
  marked `[ServerAction]` and the twin wrote or dropped it in silence, a `[ServerOnly]` accessor of a
  property or an indexer crossed with its server code, and a subscription through an interface that
  fulfils a server-only accessor reached the browser
  ([#1512](https://github.com/eQuantic/equantic-ui/issues/1512)), and an indexer or a computed property
  of an abstract or generic component, and its ordered statics, were never written
  ([#1513](https://github.com/eQuantic/equantic-ui/issues/1513)). One dispatch, `DefaultMemberWriter`,
  now writes the defaults of a class and of a record, the component's members are written on every path,
  the server skips every accessor (`IsSpecialName`), and EQ2016 refuses a `[ServerAction]` on any
  accessor and a `[ServerOnly]` on a property's or an indexer's. Proposed and archived through OpenSpec
  (`openspec/specs/transpiler-interfaces`, `openspec/specs/client-boundary`).
