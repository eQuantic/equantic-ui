# Proposal

Closes #1483, a Bug under #1174.

## Why

A `for` head's declared variable was written with no type: `for (Element? at = this; at != null; at = at.Parent)` became `for (let at = this; …)`, which TypeScript types as the class's polymorphic `this` and then refuses `at = at.parent` (TS2322). The JavaScript runs; the runtime's `tsc` refuses the module, and the element engine's walk up its parents (E3b-1b, #1477) is written exactly so. A local declared the same way had the same hole: the declared type was written only where it differed from the initializer's, and TypeScript narrows even the same type, dropping the null C# declared room for and typing `this` as the polymorphic `this`.

## What Changes

- A `for` head's declarators are written as a local declaration writes them, through the same annotation (`LocalDeclarationStrategy.Annotation`), in the head and where a captured head moves in front of the loop.
- A declaration whose type C# declared nullable, or whose initializer is `this`, says its type though the initializer has the same one: `let at: Element | null = this`.
- No public surface and no developer surface moves: only what TypeScript reads changes, and the JavaScript is the same.

## Impact

eqc's `ForStatementStrategy` and `LocalDeclarationStrategy`. No transpiled component of the runtime moves; the emission fixtures gain `EmittedChain`, which the runtime's `tsc` reads.
