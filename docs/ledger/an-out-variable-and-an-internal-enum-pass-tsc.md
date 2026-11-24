- **2026-11-24 · An out var keeps its type, and an internal enum gets its union**: two of the runtime `tsc`
  errors #1563 met:
  - eqc declared an `out var` as `any`, so a list read through one walked `unknown` elements. It now declares an
    `out var` of a named type by that type in TypeScript, and writes a miss's null as `null!`
    ([#1858](https://github.com/eQuantic/equantic-ui/issues/1858)).
  - The generator of enum unions wrote them for public enums only, while a twin imports the union of whatever enum
    it names, internal ones included. It now writes every top-level one
    ([#1859](https://github.com/eQuantic/equantic-ui/issues/1859)).

  No module's JavaScript changed. Proposed and archived through OpenSpec (`openspec/specs/transpiler-expression-variables`,
  `transpiler-classes`).
