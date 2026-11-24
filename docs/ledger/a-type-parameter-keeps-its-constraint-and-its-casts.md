- **2026-11-24 · A type parameter keeps its constraint and its casts in TypeScript**: since #1845 a generic class
  declared its type parameters by their names alone, so a member read on a `T` constrained to a class was refused by the
  runtime's `tsc`, and so was a call fed `(T)value`, written as the value alone. A type parameter constrained to a class
  that has a twin is now declared `T extends` that class, the class imported, and a cast to a type parameter is asserted
  `(value as T)`, in TypeScript only. A class the server keeps, an interface and the other constraints leave the
  parameter unconstrained, and no module's JavaScript changes
  ([#1854](https://github.com/eQuantic/equantic-ui/issues/1854), [#1860](https://github.com/eQuantic/equantic-ui/issues/1860)).
  Proposed and archived through OpenSpec (`openspec/specs/transpiler-classes`).
