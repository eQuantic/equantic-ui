# component-factories Specification

## Purpose
The declarative surface a source generator writes for an app's own components (`AppUI.g.cs`): which
components get a factory, so that the file a developer never wrote always compiles and names only
components the browser has a module for.

## Requirements

### Requirement: A factory is written for a component the surface reaches

The generated declarative surface SHALL write a factory for a concrete, non-generic component in a
namespace that is not a page only when the component and every type it is declared in are public, and
the build writes the component a module: no type it is declared in is marked `[ServerOnly]`, itself
or through a class it derives from, or `[RuntimeProvided]`, or is an attribute, and a nested component
is not marked `[ServerOnly]` either, itself or through a class it derives from, since the attribute is
inherited. A top-level component SHALL keep its factory whatever it derives from, since
the component path ignores the attribute and a top-level component always ships. The surface SHALL
compile whatever the app declares.

#### Scenario: A public component inside an owner that is not public

- **WHEN** `public sealed class Door : StatelessComponent` is declared inside an `internal` class, a
  `private` one, a `protected internal` one, or a public one nested in an `internal` one
- **THEN** the surface writes no `Door` factory, writes the other components' factories, and the app
  compiles with it

#### Scenario: A component that never crosses

- **WHEN** the class a component is declared in is marked `[ServerOnly]` or derives from a class that
  is, or the nested component is marked so itself or through its base
- **THEN** the surface writes no factory for it

#### Scenario: A component inside an attribute or a type the runtime provides

- **WHEN** a component is declared inside a class deriving from `System.Attribute`, directly or through
  another attribute, or inside a class marked `[RuntimeProvided]`
- **THEN** the surface writes no factory for it, as eqc writes it no module

#### Scenario: A top-level component and the attribute

- **WHEN** a top-level component is marked `[ServerOnly]`, or derives from a component that is
- **THEN** the surface writes its factory, as eqc writes its module

#### Scenario: A public component inside a public owner

- **WHEN** `Door` is declared public inside a public class
- **THEN** the surface writes `public static global::Demo.Vault.Door Door(string label)`, and the app
  compiles with it
