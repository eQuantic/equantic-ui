## ADDED Requirements

### Requirement: A component is a node in the browser too

A component, with or without state, SHALL be a node of the vocabulary in the browser as it is in C#:
its kind SHALL be `component` at run time, and it SHALL carry every member the vocabulary's node
declares, a type check failing when the node gains one the component lacks. The DOM escape hatch
(`HtmlElement`) SHALL NOT be one, as in C#.

#### Scenario: A component without state

- **WHEN** a component without state is made in the browser
- **THEN** its kind is `component`, and it is assignable to the vocabulary's node

#### Scenario: The escape hatch

- **WHEN** an `HtmlElement` is made in the browser
- **THEN** it carries no node kind, and the lowering lets it render itself

#### Scenario: The node gains a member

- **WHEN** the vocabulary's node declares a member the component does not
- **THEN** the runtime's type check fails
