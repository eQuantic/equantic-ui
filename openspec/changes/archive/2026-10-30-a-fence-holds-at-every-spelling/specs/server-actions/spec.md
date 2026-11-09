## ADDED Requirements

### Requirement: A Server Action is known by its symbol

A component's method SHALL be a Server Action when its symbol carries `[ServerAction]`, wherever a model
holds its tree, whichever spelling C# accepted: its body SHALL never cross, and its twin SHALL hold the stub
written from its signature. A partial method SHALL be one action.

#### Scenario: Marked through an alias

- **WHEN** `using Rpc = eQuantic.UI.Primitives.ServerActionAttribute;` marks a component's method `[Rpc]`, and
  its body reads a file
- **THEN** the build succeeds, the twin invokes `Home/Archived`, and nothing of the body crosses

#### Scenario: Named through an alias

- **WHEN** the same method is marked `[Rpc(Name = "Stored")]`
- **THEN** the twin invokes `Home/Stored`

#### Scenario: The defining half of a partial method

- **WHEN** `[ServerAction]` is written on the defining half of a partial method, and the body on the other
- **THEN** the twin holds one stub and nothing of the body
