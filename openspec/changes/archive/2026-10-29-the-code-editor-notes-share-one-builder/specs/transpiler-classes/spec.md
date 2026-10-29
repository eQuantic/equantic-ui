## ADDED Requirements

### Requirement: A class is a component by what it derives from

A class SHALL be transpiled as a component when its chain of base classes reaches a component's base, and
as a plain class when the compilation binds every base on its chain and none is a component's, whatever
its methods are called. Where a base on the chain does not bind (a reference the compilation lacks), a
class that declares `Build` or `Render` SHALL still be taken for a component, as nothing else says what it
is. The parser, which writes the twin, and the dependency resolver, which decides how it is imported, SHALL
give the same answer.

#### Scenario: A builder whose last step is Build

- **WHEN** `public sealed class NoteBuilder { public NoteBuilder(string said) { … } public VisualNode Build(bool announced) => … }`
  is transpiled with the project's compilation
- **THEN** its twin extends nothing and carries no component's type id, its `build` takes
  `announced: boolean`, and the resolver lists it among the plain classes

#### Scenario: A component over the component root itself

- **WHEN** `public sealed class Bare : UiComponent { public override VisualNode Build(ComponentContext context) => … }`
  is transpiled with the project's compilation
- **THEN** it is written as a component, `extends UiComponent` with its type id, and the resolver lists it
  among no plain classes

#### Scenario: A class of the app's named like the root

- **WHEN** `namespace App.Models { public class UiComponent { … } public sealed class Panel : UiComponent { … } }`
  is transpiled with the project's compilation
- **THEN** both are written as plain classes, with no component's type id, and the resolver lists both
  among the plain classes

#### Scenario: A class over a base the compilation cannot bind

- **WHEN** `public sealed class Legacy : MissingBase { public override VisualNode Build(ComponentContext context) => … }`
  is transpiled, and nothing the compilation reaches declares `MissingBase`
- **THEN** it is written as a component, and the resolver lists it among no plain classes

### Requirement: A member TypeScript reads as never null starts as a value TypeScript takes

A member whose start is C#'s null, or the `undefined` a vocabulary struct with no zero of its own starts
as, SHALL be said to be one where TypeScript reads the member as never null, so the runtime's strict build
takes the twin. A member that may be null SHALL keep its bare start.

#### Scenario: A struct with no zero of its own

- **WHEN** a class declares `public TypeStyle Code { get; }` and `public TypeStyle? Maybe { get; }`
- **THEN** its twin starts `this.code = undefined!;` and `this.maybe = null;`
