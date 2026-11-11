## ADDED Requirements

### Requirement: A component rebuilds when a value it depends on changes, and nothing else does

A component that reads an inherited value through `DependOn<T>()` SHALL build again in the same frame
when a parent's build hands that value's position a new node whose `UpdateShouldNotify` answers true,
and a component that read nothing of it, or read it once through `Find<T>()`, SHALL NOT. A lookup SHALL
answer the nearest value of its type above the component, and a component whose position left the tree
SHALL NOT build when the value changes.

#### Scenario: A value changes under a subtree built once

- **WHEN** a stateful holder places a new value over the same child, which holds a reader, a component
  that reads nothing and one that read the value once
- **THEN** only the reader builds again, in that frame, with the new value

#### Scenario: The value says nothing changed

- **WHEN** the new node's `UpdateShouldNotify` answers false
- **THEN** no dependent builds

#### Scenario: Two values of one type

- **WHEN** a reader stands under two values of its type, with a value of another type between them
- **THEN** it reads the nearest of its type

### Requirement: A stateful component hears a dependency change before it builds

A `StatefulComponent` that depends on a value SHALL have `OnDependenciesChanged()` called before the
build that the value's change caused.

#### Scenario: The value changes

- **WHEN** a stateful dependent's value changes
- **THEN** `OnDependenciesChanged` runs, and then its build reads the new value

### Requirement: A position's context reads the frame and the tree

A component built through an element SHALL build with a context of its position. It SHALL read the
frame's theme, density and type scale from its owner, and a frame that hands a different one SHALL
build again the components that read it and no other. It SHALL answer `InFlow` from whether an `InFlow`
is above its position. A context made with no position SHALL answer `DependOn` and `Find` with null.

#### Scenario: The density changes

- **WHEN** a frame hands a different density
- **THEN** the component that read the density builds again, and a component that read nothing of the
  frame does not

#### Scenario: InFlow

- **WHEN** one component stands under an `InFlow` and another beside it
- **THEN** the first answers true, the second false, and no ambient channel is armed

### Requirement: A component navigates the host of its tree

`Navigator.Of(context).Go(destination)` SHALL hand a non-empty destination to the navigation handler
of the owner of the component's tree, and SHALL do nothing for an empty destination or a context with
no position.

#### Scenario: A component navigates

- **WHEN** a component of a tree whose owner has a handler navigates to `/orders`, then to an empty
  destination, and a context with no position navigates
- **THEN** the handler receives `/orders` once
