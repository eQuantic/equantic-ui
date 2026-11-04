- **2026-10-30 · A fence holds at every spelling, and for what the runtime calls itself**: a Server Action
  is known by its symbol, so an alias of the attribute or a partial method crosses as its stub and never
  with its body ([#1148](https://github.com/eQuantic/equantic-ui/issues/1148)). A `[ServerOnly]` override of
  `ToString`, `Equals(object)` or `GetHashCode` on a type that crosses fails the build with EQ2010 where it
  is declared, since the runtime calls those on its own and the browser printed what .NET would not
  ([#943](https://github.com/eQuantic/equantic-ui/issues/943)), which inverts what #1146 pinned for a
  record. A type test or `as` of a package's type fails with EQ2004, where it was a null check true for any
  value ([#951](https://github.com/eQuantic/equantic-ui/issues/951)), and a refused call on a keyword is
  told once ([#953](https://github.com/eQuantic/equantic-ui/issues/953)). Proposed and archived through
  OpenSpec (`openspec/specs/server-actions`, `openspec/specs/client-boundary`).
