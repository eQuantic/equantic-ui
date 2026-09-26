# Proposal

Closes #466, a Bug under #164 (The transpiler's fences hold on every path).

## Why

The variables a C# expression declares (a pattern's bindings, an `out var`, a deconstruction's
elements) were declared by four different rules, and three of them were wrong somewhere. Measured
on main with one source run in .NET and in the modules eqc emits from it:

- A method declared every `out var` in it with one `let` at its top. A closure made in a loop read
  the last iteration's value (.NET 12, JavaScript 22 and 0), and a recursive local function
  overwrote its caller's (.NET 30, JavaScript 0).
- A getter, a setter, a field's or a property's initializer, a record's methods and accessors, and a
  component's Build declared none: a ReferenceError the first time the code ran, a module being
  strict.
- A deconstruction's elements (`(var c, var d) = …`) were declared by nothing, and a nested
  designation lost its names (`var (e, (f, g)) = …` was `let [e, ] = …`).
- Plain JavaScript, which the playground, the harness and the design host run as written, carried
  `let n: any;` in every method with an `out var` and `let money: Money = …` for a local declared as
  another type: a SyntaxError that costs the whole module.

The conformance harness declared every `out var` itself, at the top of the block, which is why none
of it showed.

## What Changes

- One owner, `ExpressionVariableScanner`, knows the variables an expression declares. It replaces
  `PatternVariableScanner`, the method-top hoisting in `OutParameters` and the primitive Render
  path's private copy, and every statement asks it, with the scope Roslyn gives:
  - an expression statement, an `if`, a `return`, a `throw`, a `yield return`, a declaration, a
    `switch`'s governing expression and a `lock` declare them in front of themselves, in the block
    they live on in;
  - a `foreach` and a `using`, whose variables Roslyn keeps inside the statement, declare them
    inside a block of their own, so two sibling loops may repeat a name;
  - a `while`, a `do` and a `for` declare them in the head's own `let` (`for (let n; cond;)`),
    which JavaScript copies for every iteration as .NET gives the condition a fresh variable;
  - a statement standing directly in a switch section leaves them to the switch, which declares
    them for its whole block, as C# scopes them;
  - a switch expression declares its arms' once, a query's clauses declare their own, and an
    initializer declares in an arrow of its own.
- Every declaration the scanner writes takes the spelling `transpiler-names` gives the variable's
  readers (#399, merged first, escapes a reserved word with a `$` on every path), and `undefined`,
  `NaN` and `Infinity` are escaped too: the emitted code compares against them, and a local of that
  name answered for the global.
- Plain JavaScript carries no annotation on a local.
- Found by the new tests: a `static` property on a plain class is written as a static getter, and a
  label on a loop whose head declares a variable stays on the loop.

For a developer using the SDK: a pattern, an `out var` and a deconstruction behave as in .NET in
every kind of member, and nothing is written differently.

The part reached is eqc. The public surface moves: `PatternVariableScanner` goes, and
`ExpressionVariableScanner` replaces it (`Declarations`, `Names`, `List`, `Scoped`,
`BindingPattern`). The developer surface does not move. The transpiled pins move where a method
declared an `out var` at its top: the `let` now stands in front of the statement that declares it.

## Capabilities

### New Capabilities

- `transpiler-expression-variables`: where the JavaScript declares a variable a C# expression
  declares, and under what name.

### Modified Capabilities

- `transpiler-names`: a local named after a global the emitted code reads is escaped.

## Impact

`src/eQuantic.UI.Compiler` (the scanner, the statement and expression strategies that hold
expressions, `TypeScriptEmitter`, `RecordTypeEmitter`, `PatternConverter`, `StringExtensions`), the
transpiled pins of seven shared components, the conformance harness, and tests in the conformance
and compiler suites.
