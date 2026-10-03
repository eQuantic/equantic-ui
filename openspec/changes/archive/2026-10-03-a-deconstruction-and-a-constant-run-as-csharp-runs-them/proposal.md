# Proposal

Closes #541, and completes #486, #483, #482, #476 and #451 on the branch that fixes them, all
sub-issues of #164.

## Why

The local review of the statements batch measured what its first cut left, each case through the
conformance runner, both sides executed:

- Only the assignment of a record went by name. `(var a, var b) = point`, a mixed declaration,
  `foreach (var (a, b) in points)` and a nested deconstruction still threw `{} is not iterable` or
  left names undeclared, and a struct's own `Deconstruct` was read by its out parameters' names,
  never run (#486).
- A long constant was inlined as a number where every long is a BigInt: `x is Limits.Five` compared
  `5n` with `5`, and `t.Ticks / TimeSpan.TicksPerDay` threw. A decimal constant in a pattern compared
  two objects with `===`, and a NaN constant never matched (#451).
- `o is char` was true for any string (#482).
- A component declared a static `field` store alone, so it read undefined until the first write, a
  class dropped the property's initializer, and a static auto-property with no initializer read
  undefined where C# reads 0 (#483).
- A loop moved in front for a closure left what its initializer declares behind, after the code that
  assigns it (#476).
- A local function kept its `out` parameters as plain ones, while its call unwraps the object every
  method with outs returns (#541).

## What Changes

- One lowering for a deconstruction, read from the bound tree's `DeconstructionInfo`, for the
  declaration, the assignment and the foreach: by position for a tuple and a dictionary's pair, by
  the names of the `Deconstruct` each level calls otherwise, and a `Deconstruct` the app wrote is
  called at the top level.
- A long or a ulong constant is a BigInt literal. A constant pattern in either spelling goes through
  one test: a null is any absence, a decimal and a NaN compare by value, anything else is `===`.
- A char tests as a string of one code unit.
- A static store starts as its initializer or its type's default, in a component and in a class.
- A hoisted loop declares what its initializer declares with its own variables, once.
- A local function with outs follows the callee contract, through one owner it shares with the
  lambda.

## Capabilities

### New Capabilities

(none)

### Modified Capabilities

- `transpiler-expressions`: a constant in a pattern, and the char type test.
- `transpiler-records`: a deconstruction in every shape, and a static store's first value.
- `transpiler-statements`: a hoisted loop's initializer variables, and a local function's outs.

## Impact

- **eqc**: `DeconstructionPattern` (new), `AssignmentExpressionStrategy`,
  `ForEachVariableStatementStrategy`, `SemanticHelper.GetDeconstructionInfo` (a new public member),
  `InlinedConstantStrategy`, `PatternConverter`, `IsPatternStrategy`, `TypeScriptEmitter`,
  `ForStatementStrategy`, `OutParameters`, `LambdaExpressionStrategy`,
  `LocalFunctionStatementStrategy`.
- **Left out**: a deconstruction into an indexer, or into a wider type (#542), which predates this
  for tuples.
