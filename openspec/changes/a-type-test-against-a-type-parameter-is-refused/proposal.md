## Why

Closes #1846, a Bug under #1587, found by #1563.

A type test against a type parameter was answered as a null check. `recognizer is T`, inside
`GestureRecognizerFactoryWithHandlers<T>`, was `recognizer != null`, so a factory of one recognizer said it made every
one, with nothing reported. `PatternConverter.TypeCheck` gave every type it did not know its last line, and a type
parameter reached it, through `x is T`, `x as T`, a type pattern `T t` and a `catch (T e)` alike.

The browser cannot answer it. A closed construction is marked with its type arguments as text (`$eq.closing`, from
`ObjectCreationStrategy.ClosureOf`), and "is a `T`" takes a subclass, which no text can answer. Whether generic code
carries its type arguments at run time is #755's design question.

## What Changes

For a developer using the SDK: a type test against a type parameter fails the build with EQ2016. The diagnostic names
the parameter, says why, and suggests passing the test in (a `Func<object, bool>`, or the `Type` to compare) or testing
for a class. Before, it compiled and answered true for any value that was not null.

- `PatternConverter.TypeCheck` refuses a type parameter with EQ2016 before its last line.
- `docs/DIAGNOSTICS.md` and the wiki's Diagnostics page, in every language, gain the row. Their section now runs to
  EQ2016.

Parts reached: eqc only. No public surface moves. The developer surface gains a refusal: code that compiled with a wrong
answer now fails the build, which preview allows, and the row says what to write instead.

Rules followed: CLAUDE.md's, that a construct the model cannot answer is a build error, never a guessed translation.

## Capabilities

### New Capabilities

(none)

### Modified Capabilities

- `transpiler-expressions`: "A type pattern tests the type" refuses a type parameter with EQ2016.

## Impact

`tests/eQuantic.UI.Compiler.Tests` gains `TypeParameterTypeTestTests`. Five forms are refused, none of them on main,
and a test against a class is not. The diagnostics baseline records EQ2016's reporting site.
