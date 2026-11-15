## ADDED Requirements

### Requirement: A declared variable keeps the type C# declared

A variable a local declaration or a `for` head declares SHALL be written with the type C# declared wherever TypeScript would infer a narrower one from its initializer: a type C# declared nullable, a `this` initializer, which TypeScript types as the class's polymorphic `this`, and a wider declared type.

#### Scenario: A for head that walks up from this

- **WHEN** a method declares `for (EmittedChain? at = this; at != null; at = at.Parent) depth++;`
- **THEN** the twin writes `for (let at: EmittedChain | null = this; …)`, and the runtime's `tsc` accepts it, where `let at = this` fails with TS2322
