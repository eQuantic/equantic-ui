- **2026-11-23 · A hand-written twin's config takes every member its C# class lets an initializer set**: a
  transpiled initializer reaches a hand-written twin as its trailing config, and nothing compared that config with
  the C# class, so a member it lacked was a TS2353 the runtime's `tsc` raised only once a component first set it.
  `BoxStyleConfig` lacked `foreground` until a slice of #1606 met it. `VocabularyConfigShapeTests` now reads every
  config the mirror's twins take, named or written in place, against its C# class both ways, with a baseline that
  may only shrink; on main it found 183 members apart, 53 of them members a constructor also takes, which eqc
  writes into the config when an initializer sets them (`new Image(src, 1, 2) { Label = "x" }`). The configs gain
  `foreground`, a `VisualNodeConfig` and a `SingleChildNodeConfig` the node configs extend or intersect, and every
  member their constructors take, all type-only. What eqc writes for eight such initializers joins the runtime's
  fixtures, which its `tsc` checks
  ([#1823](https://github.com/eQuantic/equantic-ui/issues/1823)). Proposed and archived through OpenSpec
  (`openspec/specs/transpiler-classes`).
