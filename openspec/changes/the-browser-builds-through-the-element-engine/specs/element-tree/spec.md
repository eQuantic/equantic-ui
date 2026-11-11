## ADDED Requirements

### Requirement: The browser builds through the element engine

The browser SHALL build every component of a page through the element engine's owner, and its lowering
SHALL lower what each position built and keep the instance each position keeps, without building again.
A nested component's state change SHALL build that component's element and no other before the page
lowers again. Hydration SHALL build the element tree before it walks the server's DOM, and the server's
state for a component SHALL reach its instance before that instance first builds.

#### Scenario: A nested state change builds only its own element

- **WHEN** a page holds two stateful components side by side and one of them changes its state
- **THEN** that component builds again and neither its sibling nor the page builds

#### Scenario: A page's own state change builds what it composes

- **WHEN** a page changes its own state and hands a nested stateful component new configuration
- **THEN** the nested component keeps its instance and its state, and builds with the new configuration

#### Scenario: Hydration adopts the server's state before the first build

- **WHEN** a page the server drew with a component's loaded data hydrates
- **THEN** the component builds first with that data, under the `Type#ordinal` the server wrote it with

#### Scenario: A component that throws costs its own subtree

- **WHEN** a nested component's build throws
- **THEN** its position draws the failure and the rest of the page draws as before
