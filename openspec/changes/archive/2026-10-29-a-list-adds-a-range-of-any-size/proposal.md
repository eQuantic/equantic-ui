## Why

Closes #1060, a bug under #849 (the transpiler's fences hold on every path, part three). eqc lowered
`list.AddRange(items)` to `list.push(...items)` and `list.InsertRange(index, items)` to
`list.splice(index, 0, ...items)`. A spread hands every element to the call as an argument, and an engine
caps a call's arguments by its stack. Measured on this machine with a spread into `push`: JavaScriptCore,
Safari's engine and the Bun the SDK embeds, takes about 638,000 and V8 about 124,000. A million elements
then threw a RangeError in every browser, where .NET adds any number, and nothing said so at build time.
The lines of a large file, the rows of a sheet and a chart's points cross that easily.

Measured in the conformance suite at a million elements, the other shapes a list or a sequence takes are
not affected: `Concat`, `ToList`, `new List<T>(items)` and a collection expression's `..items` spread into an
array literal, which has no cap, `RemoveAll` is the runtime's single pass, and a bound `Max` and `Min` walk
the values in the runtime. The same cases at 100,000 pass in both engines, since both caps are higher.

The same spread was wrong in four more ways, each measured: a null collection threw a TypeError instead of
.NET's ArgumentNullException, a string added its code points where .NET adds its UTF-16 chars, an index past
the end or below zero inserted where .NET refuses it, and a named argument was put in the place of the one
written first: `InsertRange(collection: c, index: i)` spread the index.

## What Changes

For a developer using the SDK, nothing to write: a list's `AddRange` and `InsertRange` take a range of any
size in the browser, as they do in .NET.

- **The runtime adds and inserts one element at a time.** `$eq.collections.addRange` and `insertRange`
  copy the collection as `new List<T>(collection)` copies it (an array as it is, a string's chars, a null
  refused by its name), check the index as .NET checks it, after the collection, and never spread. A list
  added to or inserted into itself takes what it held before the call, as .NET's does.
- **eqc calls them for the overload C# binds**, each argument in the parameter it fills and evaluated in the
  order it is written, a named collection before a named index included.
- **A sorted set's view is read as a collection by a range.** The runtime's copy asks a view its count, as
  .NET's `AddRange` and `InsertRange` ask it, which closes a divergence the `runtime-collections` spec named.
- **With no model to bind the call, `Max` and `Min` fold the values** with `Math.max` and `Math.min`
  instead of spreading them into one call, keeping the numeric answer that path always had.

The parts it reaches: eqc (`ListMethodStrategy`, `MinMaxStrategy`, two names in `Eq`) and the runtime
(`utils/list.ts`). The compiler's public surface gains the two names (`Eq.ListAddRange`,
`Eq.ListInsertRange`), a tool assembly whose surface is never promised; the developer surface does not move.
Nothing an app wrote breaks. What changes in the browser is the five cases above, each of which now answers
as .NET does.

A spread of a whole array into a `params` parameter, `f(...items)`, meets the same cap. It is a calling
convention rather than a list's lowering, a rest parameter on the twin's side, and is left to an issue of its
own.
