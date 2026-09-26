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
| `while`, `do`, `for` | not in scope | the head's own `let`, one per iteration |
| `foreach`, `using` | not in scope | a block around the statement |

A statement standing directly in a switch section is the exception: C# scopes what it declares to
the whole switch block, so another section may assign and read it, and the switch declares those
names once, at the top of its block. Declared in the section that wrote it, a `let` was in its
temporal dead zone for every other section.

An embedded statement (`if (c) M(out var x);`) is its own scope in C#, and the statement writer
already braces a statement that becomes two.

.NET gives a loop's condition a fresh variable every time round: a closure over a while's or a
for's `out var` keeps its own iteration's value (12, where one slot answered 22 or 0). A `for`
head's `let` is the one JavaScript binding that is copied for each iteration, so the variable goes
there, and a `while` with one becomes that `for`. A `do` does too, as
`for (let n, $again = true; $again; $again = cond)`: the flag runs the body first and the condition
after it, a `continue` included, and the condition assigns its variables in the iteration's own copy.
A `for` whose head holds expressions rather than a declaration runs them as the initializer of one
more binding, `$init`, which no C# name can take, and a deconstruction there that declares itself
joins the head's `let`.

## A query's clauses are their own scope

C# scopes a variable declared in a query clause to the clause, and eqc lowers each clause to an
arrow whose body declares its own. The scanner therefore skips a query's body and walks only its
source, so two queries in one block may bind the same name.

## An initializer runs outside any statement

A field's or a property's initializer has no statement to declare in front of, and each one is its
own scope in C#, so two may bind the same name: the constructor they are written into could not take
both as siblings. Each is wrapped in an arrow that declares what it declares and returns its value,
only when it declares something.

## A name is spelled once

A declaration the scanner writes is spelled by `ToJsIdentifier`, as every reference to the variable
is, so the two cannot drift. #399, merged while this was under review, made that function escape a
reserved word with a `$` on every declaration path, which is the spelling kept here.

A switch's subject was bound to `_s`, a name a C# local can take. Now that a section's and an arm's
variables are declared in the switch's own scope, a local called `_s` there was a second declaration
of it, so the subject is `$s`, the `$` no C# identifier holds. That is the slice of #397, which moves
every synthesized name to a `$`, that this change made reachable; the rest stays there, the globals
the emitted code reads included.

## Not here

- A `catch` clause ignores its exception type and its filter, and two clauses do not parse (#474):
  a filter's variables have nothing to declare until the filter is converted.
- A lock's expression runs only when it declares something the body may read; that it is otherwise
  dropped is #475.
- A `for` loop's own variable is one per loop in C# and one per iteration in the emitted `for`
  (#476). The head's expression variables are the other way round, and this change gives them their
  own iteration.
- A constructor that chains with `: this(…)` is outside what the emitter lowers today.
