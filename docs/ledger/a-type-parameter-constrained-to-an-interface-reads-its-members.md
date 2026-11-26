- **2026-11-26 · A type parameter constrained to an interface reads its members in TypeScript**: since #1854 a constraint
  to a class with a twin was written `T extends` that class, but an interface has no twin, so a type parameter
  constrained to one, the most common constrained generic in C#, stayed unconstrained and the runtime's `tsc` refused a
  member read on it. Every in-source interface whose twin can be written now gets one, a module holding
  `export interface` with each member's twin name and type beside a value of its name, and a constraint to it is written
  `T extends` it, imported. A generic interface, one with a static, default, event or indexer member, one extending
  another and one the server keeps get none and leave the constraint unconstrained. A value typed as an interface stays
  `any`, and the runtime library gains the twins of seven of its own interfaces
  ([#1878](https://github.com/eQuantic/equantic-ui/issues/1878)). Proposed and archived through OpenSpec
  (`openspec/specs/transpiler-classes`).
