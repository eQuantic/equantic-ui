## ADDED Requirements

### Requirement: A component's store a property guards with field starts as C# starts it

A component's instance property that keeps its store through `field` SHALL have that store start, when the component is built, as C# starts the backing field: the property's initializer, written to the store without its setter, or its type's zero, a reference's null included.

#### Scenario: A component built with no configuration

- **WHEN** a component declares `public int Level { get => field; set => field = value * 2; }` and `public string Label { get => field; set => field = value; }`, and is built with nothing written to either
- **THEN** `Level` reads 0 and `Label` reads null, as in .NET; they read undefined

#### Scenario: An initializer past the setter

- **WHEN** a component declares `public int Start { get => field; set => field = value + 1; } = 5`
- **THEN** `Start` reads 5, as in .NET, and 2 after `Start = 1`
