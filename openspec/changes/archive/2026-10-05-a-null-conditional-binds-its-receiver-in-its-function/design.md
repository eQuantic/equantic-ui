# Design

## Context

`ConditionalAccessStrategy` rebuilds a null-conditional tail on a placeholder receiver and converts
it with the ordinary strategies. When the translation starts with the placeholder, the receiver
goes back in front as JavaScript's own `?.`. When it does not (a helper call such as
`$eq.text.trim(…)`, a spread), the receiver has to be read once and tested, and the tail has to stay
lazy. #536 did that with a conditional for a receiver it can read again and kept an arrow invoked on
the spot for every other one, refusing the arrow when the tail awaits.

Flutter has no row for this: Dart compiles null-aware access itself. The mechanism to imitate is
C#'s own. Roslyn lowers `a?.M(x)` to a synthesized LOCAL of the method, `temp = a; temp != null ?
temp.M(x) : default`, and when the method is async that local is hoisted with the others, so an
`await` in `x` is the method's own. A JavaScript `let` in the function the C# is written in is the
same slot.

## Goals / Non-Goals

**Goals:**
- No function around a null-conditional tail wherever a statement or a body can declare a slot.
- One slot per call of the function the C# is written in, so concurrent calls of an async lambda
  never share one.
- A declaration that is valid JavaScript wherever a statement can stand.

**Non-Goals:**
- Moving `ConditionalAccessStrategy` onto the IR. It stays a text strategy in
  `ir-migration.baseline.txt`; the change is where the receiver lives, not how it is written.
- The null-conditional ASSIGNMENT (`a?.B = v`), which has its own lowering.

## Decisions

**A temporary declared by the statement, not by the function's first line.** The converter already
has one dispatcher every statement passes through, so it opens a scope around each statement's
conversion and declares what was bound in front of the statement, the way a pattern variable is
hoisted. The alternative, a declaration at the top of each function, would have to be taught to
every writer of a function body (the emitters, the record emitter, the local functions), several of
them in flux on another branch.

**A concise body converted in a scope of its own.** `ConvertExpressionBodyIr`, the one lowering of a
member's, a local function's and a declaring lambda's concise body, declares what it bound in its
block. A lambda that declares nothing is still converted as an expression, in a scope of its own,
and is given the block only when its body bound a temporary, so every other lambda keeps its shape.
Without that scope the slot would land in the statement around the lambda, shared by every call.

**A label keeps its loop.** A statement a label names does not open its own scope: what it binds is
declared in front of the label. Declared between them, the writer braces the loop away from its
label and `continue outer` is a SyntaxError (measured: bun refuses the module).

**A translation that names a temporary is never served from the node cache.** Served again, it
would name a slot only the first statement declares. It is converted afresh, binding a fresh slot
where it stands. No path in the suites converts such a node twice today, so a test drives it
directly through two conversions of one statement.

**Fresh names, `$n0`, `$n1`…, numbered per module.** C# has no `$` in a name, and the compiler's
other temporaries use other letters. Each null-conditional takes its own name before its tail is
converted, so a guard in the tail of another binds a different one.

**The arrow stays where no statement can declare a slot**: an initializer and a record's base call,
which C# never lets await. The introduced-functions baseline records it under a new reason, "no
statement there", rather than "not looked at yet".

## Risks / Trade-offs

- [A strategy that converts C# into a function of its own without going through the dispatcher or
  the concise-body lowering (a query clause, a switch expression's arrow) binds its slot in the
  statement around it] → those functions are synchronous and run within one call of the enclosing
  function, and the receiver is read before any argument runs, so one slot per statement is enough
  there; an asynchronous one goes through the lambda's scope.
- [A concise lambda that binds a slot is converted at the arrow's depth and placed in a block one
  level deeper] → only the indentation of a multi-line expression inside it is off by one level; the
  JavaScript is the same.
