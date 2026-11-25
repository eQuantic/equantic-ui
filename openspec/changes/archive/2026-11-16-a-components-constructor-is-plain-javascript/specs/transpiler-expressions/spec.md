## MODIFIED Requirements

### Requirement: Plain JavaScript carries no annotation

A module eqc writes without type annotations, as the design host asks, SHALL hold no TypeScript
syntax, a local's declared type and a component's constructor parameters included.

#### Scenario: A local that starts null

- **WHEN** `string? label = null;` is compiled with type annotations off
- **THEN** it is written `let label = null;`, and with them on `let label: string | null = null;`

#### Scenario: A component that takes its parameters through its constructor

- **WHEN** `Tally(string tag, int size = 2, params int[] items)`, a component, is compiled with type
  annotations off
- **THEN** its twin's constructor is written `constructor(tag, size = 2, items, props)`, bun loads the
  module, and `new Tally("t")` reads an empty `items` as .NET does; with them on it is written
  `constructor(tag?: any, size: any = 2, items?: any, props?: any)`
