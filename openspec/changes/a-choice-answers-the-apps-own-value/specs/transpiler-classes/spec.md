## ADDED Requirements

### Requirement: A generic type's TypeScript twin passes the runtime's type check

The twin eqc writes for a generic record, class or component SHALL pass the runtime's strict `tsc` (its
`noUnusedLocals` and `noUnusedParameters` included), and SHALL write the same JavaScript it wrote before. A
type parameter SHALL never be imported as a module. A twin whose text writes a type parameter in a type
position SHALL declare that parameter, and a reference to a generic twin with type arguments SHALL match
the declaration it names. A type parameter a twin declares SHALL be one its text uses.

#### Scenario: A generic record of the shared library

- **WHEN** the shared library declares `public sealed record Choice<T>(T Value, string Label)` and a factory
  `public static Tabs<T> Tabs<T>(IReadOnlyList<Choice<T>> items, T value, Action<T>? onChanged = null)`, and
  the runtime is type-checked (`dotnet build src/eQuantic.UI.Runtime -t:TestRuntime`)
- **THEN** `tsc` accepts `Choice.ts` and `UI.ts`, where the twin of today wrote `declare value: T` in a
  class that declares no `T` (TS2304) and `UI.ts` wrote `Choice<T>[]` of a class that is not generic
  (TS2315)

#### Scenario: A generic component of the shared library

- **WHEN** the shared library declares `public sealed class Tabs<T> : StatelessComponent` holding a `T`
  and an `Action<T>`, and the runtime is type-checked
- **THEN** its module imports no `T` and `tsc` accepts it, where the twin of today wrote
  `import { T } from "./T"` (TS2307) and declared a `T` none of its members used (TS6133)

#### Scenario: The JavaScript does not move

- **WHEN** an app's generic component and the library's generic types are transpiled before and after
- **THEN** the JavaScript bun writes from each is the same, byte for byte
