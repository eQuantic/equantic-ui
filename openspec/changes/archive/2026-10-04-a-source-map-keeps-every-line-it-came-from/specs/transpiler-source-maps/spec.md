# Spec Delta

## MODIFIED Requirements

### Requirement: A lambda's block maps statement by statement

Every statement of a lambda's or a `delegate`'s block that reaches the writer as IR SHALL map to its
own C# line, wherever the writer places the arrow: a call's argument, a LINQ operator's template, a
local's initializer, an arrow inside an arrow, a member's expression body, an object creation's
argument, an object or collection initializer's value and an anonymous object's member. The block the
emitter adds to a concise body that declares a variable SHALL map to that body. What follows the block
on its closing line SHALL map to the statement that holds the arrow. Writing the marks SHALL change
nothing the writer writes.

#### Scenario: A block lambda passed to List.ForEach

- **WHEN** `values.ForEach(value => { var twice = Twice(value); total += twice; });` is compiled with
  each statement on its own line
- **THEN** the map leads `let twice = this.twice(value);` and `total += twice;` to their own C#
  lines, where neither line had a segment

#### Scenario: A frame thrown inside a lambda's block

- **WHEN** a method called from a statement inside a `List.ForEach` block throws, in the module a
  build bundles with the embedded Bun
- **THEN** the lambda's frame leads, through the composed map, to the statement that called, not to
  the line that holds the lambda

#### Scenario: A frame after a lambda's block

- **WHEN** `var kept = values.FindAll(value => { var doubled = value * 2; return doubled > 0; }).Count + Check(count);`
  runs, its block on lines of its own, and `Check` throws
- **THEN** the frame that called `Check` leads to the statement, not to `return doubled > 0;`, the
  block's last statement

#### Scenario: A lambda an expression-bodied member holds

- **WHEN** `public void Bump(List<int> values) => values.ForEach(value => { var doubled = value * 2; _seen += doubled; });`,
  an expression-bodied getter, setter and constructor, and an expression-bodied `Build` that
  constructs a node with a handler, each hold a lambda with a block
- **THEN** the map leads each statement of every block to its own C# line, where none had a segment
  and each read as the member's expression

#### Scenario: A lambda an object creation or an initializer holds

- **WHEN** `new Rule("built", text => { var length = text.Length; return length > 2; }) { Changed = changed => { var flag = changed ? 1 : 0; _seen = flag; } }`
  and `public static Rule Email(string message = "invalid") => new(message, address => { … });` are compiled
- **THEN** the map leads each statement of the argument's, the initializer's and the target-typed
  creation's block to its own C# line, where none had a segment

#### Scenario: A frame thrown inside a lambda a creation or an expression body holds

- **WHEN** a method called from a statement inside a lambda held by `new Step(value => { … })`, or by
  the expression body `=> Apply(count, value => { … })`, throws, in the module a build bundles with
  the embedded Bun
- **THEN** the lambda's frame leads, through the composed map, to the statement that called, not to
  the line that holds the creation or the expression body

#### Scenario: The shared components keep every lambda's lines

- **WHEN** the shared components and models are transpiled as an app's build transpiles them
- **THEN** no statement inside a lambda's or a `delegate`'s block lacks a segment of its own, where
  59 did

## ADDED Requirements

### Requirement: A body a lowering wraps maps statement by statement

The statements of a body the transpiler wraps in code of its own SHALL each map to their own C# line:
a method's, a lambda's and a local function's body with an `out` or a `ref` parameter, which runs in an
arrow so that its returns keep their meaning, and an iterator's body, which fills the buffer it
returns. The lines the wrapper adds SHALL map to the declaration whose body it wraps.

#### Scenario: A method with an out parameter

- **WHEN** `public bool TryHalf(int value, out int half) { var doubled = value * 2; half = doubled / 4; return value % 2 == 0; }`
  is compiled, with a `ref` method, a lambda with an `out` parameter and a local function with one beside it
- **THEN** the map leads each statement of every body to its own C# line, where the first shared
  the wrapper's line and none had a segment

#### Scenario: A frame thrown inside a body with an out parameter

- **WHEN** a statement inside `TryHalf(int value, out int half)` throws, in the module a build bundles
  with the embedded Bun
- **THEN** the top frame leads, through the composed map, to the statement that threw, where it led to
  the method's head, and the caller's frame to the call

### Requirement: A map names every file its segments come from

A module's map SHALL name, among its sources, every file a segment of it comes from, each once and
named as the project names it, and each segment SHALL name the file it comes from. A map that carries
its C# SHALL carry each file's own text.

#### Scenario: A default an interface supplies

- **WHEN** `class Form : IValidating` takes the default `bool Validate(List<string> errors)` that
  `IValidating.cs` supplies, and Form.cs is compiled with both files in the compilation
- **THEN** the map names Form.cs and IValidating.cs, each with its own text, and leads the default's
  statements to their lines in IValidating.cs, where it led them to lines of Form.cs that Form.cs does
  not have

#### Scenario: A frame thrown inside a default an interface supplies

- **WHEN** a statement inside a default `IChecked` supplies throws, called from the class that takes
  it, in the module a build bundles with the embedded Bun
- **THEN** the top frame leads, through the composed map, to the throw's line in IChecked.cs, and the
  next to the call in the class's file
