## ADDED Requirements

### Requirement: A downcast to a type of the runtime keeps its type whichever class declares the member

A downcast to a type the runtime carries a twin of SHALL keep its target's type for the checker, whichever class declares the member the cast reaches, and SHALL write the value alone only where that type is itself the server's (`[ServerOnly]`).

#### Scenario: A member inherited from a base the server keeps

- **WHEN** code reads `((Pressable)node).Child`, `Child` declared on `SingleChildNode`, a `[ServerOnly]` base
- **THEN** the twin writes `(node as Pressable).child`, `Pressable` imported from the runtime; it wrote `node.child`

#### Scenario: A flex node's children

- **WHEN** code reads `((Row)node).Children` and `((Column)node).Children`, declared on `FlexNode`
- **THEN** the twin writes `(node as Row).children` and `(node as Column).children`

#### Scenario: A cast to the server's own base

- **WHEN** code casts to `FlexNode`, itself `[ServerOnly]`
- **THEN** the twin writes the value alone, as it did
