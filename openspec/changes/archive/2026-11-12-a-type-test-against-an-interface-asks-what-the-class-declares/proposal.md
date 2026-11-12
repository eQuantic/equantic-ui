# Proposal

Closes #1464, a Bug under #1174.

## Why

eqc translated a type test against an interface (`x is IFoo`, `x as IFoo`, a type pattern `IFoo f`) as `x != null`, so it answered true for any value that is not null, with no diagnostic. The element engine's `(state as IDisposable)?.Dispose()` called a `dispose` that no state had, and threw when a position left the tree, which the browser's node state (E3b-2) is about to reach.

## What Changes

- The twin of every class, record, struct and component the compiler writes declares in `static $interfaces` the interfaces a type test can ask of it, those of its bases included, by the name .NET gives each one, and a test against one of them reads it (`$eq.types.implements`), as .NET answers off the type: a value whose class declares none implements none, whatever members it has.
- A test can ask for an interface of the app's own or of a project it references, one of an assembly the runtime carries transpiled whole (the code and sheet engines, the component and chart libraries), and `IDisposable`, since only classes the compiler writes implement those.
- Every other test against an interface is refused with EQ2015, rather than guessed: an interface with type arguments, which JavaScript erases, so `IFoo<int>` could not be told from `IFoo<string>`; one that values the browser holds as its own (a string, a number, an array) implement, which could not say so; and one of the visual vocabulary, whose classes the runtime writes by hand.
- The compiler declares `Eq.TypesImplements`, a tool's surface. No contract member and no developer surface moves.

## Impact

eqc (`PatternConverter`, the class, record and component emitters, the new `InterfaceTests`) and the runtime (`utils/runtime-types.ts`, `$eq.types.implements`). The code engine's transpiled twins gain their markers (`ICodeLanguage`, `ICodeCompletionProvider`, …). A build that tested an interface the browser cannot answer now fails with EQ2015 where it ran a test that always passed; nothing in this repository does.
