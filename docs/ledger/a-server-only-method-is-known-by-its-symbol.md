- **2026-10-29 · A [ServerOnly] method is known by its symbol, so an alias or a partial definition keeps it out of every twin**:
  a bug under #849 ([#1140](https://github.com/eQuantic/equantic-ui/issues/1140)). Whether a method stays on the
  server was read off its own attribute lists by name, and two spellings C# accepts carry the attribute where those
  lists do not show it: an alias of `ServerOnlyAttribute`, and the defining half of a partial method, whose
  attributes C# merges with the implementing half's. Measured on main for a class, a static class, a record, a
  struct, a static class nested in a component and a component, built as eqc builds an app and compiled on its own:
  each wrote the method into its twin with its body, named the app type the body constructs, and failed the build
  with EQ2112 and EQ2103; the EQ1007 guard refused such a method beside its overload. The rule asks the method's
  symbol now wherever a model holds its tree, and every reader hands it its model: the parser, the emitters, method
  lowering, the import and refusal scans and the overload guard. A member that writes no attribute and is no
  partial method binds nothing. The same proof found a record that takes a `[ServerOnly]` interface default
  refused with EQ1008, however the attribute was spelled, where a class left the default out, and Copilot's review
  found a private helper only such a default calls crossing with it (EQ2112) and the EQ1007 guard counting the
  default: the shared discovery of defaults leaves a server-only one out now, before its helpers are collected. Proposed and archived through OpenSpec (`client-boundary`).
