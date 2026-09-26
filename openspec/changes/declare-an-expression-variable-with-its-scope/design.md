# Design

## Flutter has nothing to answer here

Dart has no `out` parameter, and dart2js declares a Dart local where Dart declares it. That is the
rule this change adopts for C#: a variable is declared where C# declares it, with the scope C# gives
it, and no wider.

## One owner, asked by every statement

The variables an expression declares are the same question for a pattern's binding, an `out var`
and a deconstruction element, and the C# specification calls all three expression variables. The
scanner answers it once: it walks an expression without entering a lambda, an anonymous method or a
local function (their bodies are their own scope, declared per call by their own statements) or a
switch expression's arm (the arrow that strategy emits is the arm's scope), and it leaves out a
deconstruction that declares itself (`var (a, b) = …` is `let [a, b] = …`).

## The scope is Roslyn's, measured

For each statement kind, Roslyn was asked whether a variable declared in it can be used after it
and redeclared beside it. The answers decide where the `let` goes:

| Statement | After it | Where the `let` goes |
| --- | --- | --- |
| expression statement, `if`, `return`, `throw`, `yield`, declaration, `switch`, `lock` | usable | in front, in the enclosing block |
| `while`, `for` | not in scope | the head's own `let`, one per iteration |
| `do`, `foreach`, `using` | not in scope | a block around the statement |

An embedded statement (`if (c) M(out var x);`) is its own scope in C#, and the statement writer
already braces a statement that becomes two.

.NET gives a loop's condition a fresh variable every time round: a closure over a while's or a
for's `out var` keeps its own iteration's value (12, where one slot answered 22 or 0). A `for`
head's `let` is the one JavaScript binding that is copied for each iteration, so the variable goes
there, and a `while` with one becomes that `for`. A `for` whose head holds expressions rather than a
declaration runs them as the initializer of one more binding, `$init`, which no C# name can take.

## An initializer runs outside any statement

A field's or a property's initializer has no statement to declare in front of, and each one is its
own scope in C#, so two may bind the same name: the constructor they are written into could not take
both as siblings. Each is wrapped in an arrow that declares what it declares and returns its value,
only when it declares something.

## A name is spelled once

`ToJsIdentifier` renames a C# local that JavaScript refuses (`class` → `class_`); a reference
already went through it. Its list left out the keywords on the reasoning that C# reserves them too,
but the verbatim escape makes each of them a name. Every declaration site now goes through it, as
the references do.

## Not here

- A `catch` clause ignores its exception type and its filter: its filter's variables have nothing
  to declare until the filter runs (filed separately).
- A lock's expression is evaluated only when it declares something the body may read; that it is
  otherwise dropped is filed separately.
- A constructor that chains with `: this(…)` is outside what the emitter lowers today.
