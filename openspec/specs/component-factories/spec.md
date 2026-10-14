# component-factories Specification

## Purpose
The declarative surface a source generator writes for an app's own components (`AppUI.g.cs`): which
components get a factory, so that the file a developer never wrote always compiles and names only
components the browser has a module for.

## Requirements

### Requirement: A factory is written for a component the surface reaches

The generated declarative surface SHALL write a factory for a concrete, non-generic component in a
namespace that is not a page only when the component and every type it is declared in are public and
none of them is marked `[ServerOnly]`. The surface SHALL compile whatever the app declares.

#### Scenario: A public component inside an owner that is not public

- **WHEN** `public sealed class Door : StatelessComponent` is declared inside an `internal` class, a
  `private` one, a `protected internal` one, or a public one nested in an `internal` one
- **THEN** the surface writes no `Door` factory, writes the other components' factories, and the app
  compiles with it

#### Scenario: A component that never crosses

- **WHEN** the component, or the class it is declared in, is marked `[ServerOnly]`
- **THEN** the surface writes no factory for it

#### Scenario: A public component inside a public owner

- **WHEN** `Door` is declared public inside a public class
- **THEN** the surface writes `public static global::Demo.Vault.Door Door(string label)`, and the app
  compiles with it
