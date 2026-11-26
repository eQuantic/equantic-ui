- **2026-11-26 · An `out var` keeps its type in every scope that declares one**: since #1858 an `out var` of a named
  type was declared by its type in TypeScript only by the statements that put it in the enclosing block, and a loop's
  condition, a `using`, a `catch` filter, a switch's sections and guards, a switch expression's arms, an initializer and
  a constructor's base call still declared it `any`, so a list walked through it was refused by the runtime's `tsc`.
  Each scope now types it from the expression that declares it, and a loop's head starts it as `undefined!`, since a
  definite assignment is no part of that grammar. No module's JavaScript changes
  ([#1865](https://github.com/eQuantic/equantic-ui/issues/1865)). Proposed and archived through OpenSpec
  (`openspec/specs/transpiler-expression-variables`).
