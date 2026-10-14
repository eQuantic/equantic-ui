- **2026-10-14 · What eqc writes names what C# declares**: a name spelled with C#'s `@` went out as it
  was spelled, so `public static class @Shelf` was `export class @Shelf` in a module named `@Shelf`, a
  record printed `@Point { X = 1 }`, a `new Store.@Failure()` imported nothing and a member typed `@Point`
  was annotated `@Point`; a field moved aside for a member a case apart only in its own type, so a derived
  class's `Amount` was hidden by the base's `amount` and answered the field; a lambda over a `long` or a
  `decimal` was annotated `number`, which the runtime's `tsc` refuses; and a public component nested in a
  class the generated file cannot reach, or in a `[ServerOnly]` one, got a factory that did not compile.
  Every name eqc reads from syntax is now its value, guarded by a test that refuses `Identifier.Text`
  and, asked of the model, `ToString()` on a type's syntax; a field moves aside for a member any class
  derived from it in its project declares, and one in another project is refused with EQ1007; a lambda's
  parameter is annotated `bigint` and `Decimal`; and a factory is written only for a component that
  crosses. Fifteen verbatim-name cases and three inherited-member cases executed on both sides, two
  referenced-project tests, the generator's six unreachable owners and the shared library's `tsc` pin
  them, and each bug failed them before
  ([#704](https://github.com/eQuantic/equantic-ui/issues/704),
  [#740](https://github.com/eQuantic/equantic-ui/issues/740),
  [#758](https://github.com/eQuantic/equantic-ui/issues/758),
  [#703](https://github.com/eQuantic/equantic-ui/issues/703)). Proposed and archived through OpenSpec
  (`openspec/specs/transpiler-names`, `transpiler-classes`, `transpiler-expressions`,
  `component-factories`).
