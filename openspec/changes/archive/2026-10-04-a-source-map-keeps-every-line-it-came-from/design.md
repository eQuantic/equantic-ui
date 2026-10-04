# Design

## How Flutter answers it

It has no direct bearing, as for #384. Dart's compilers print JavaScript from a tree, so no body
becomes text before the printer reaches it, and a map names every Dart file its output came from.
docs/FLUTTER-PARITY.md has no row for source maps.

## Decisions

- **A wrapper is built where its body is, as IR.** The arrow an `out` or `ref` parameter's body runs
  in is a lowering of eqc's, so its lines belong to no C# statement, and its body's statements are
  C#'s. Built as text, the wrapper took the body's origins with it. Built as IR (`OutParameters.Body`),
  the arrow's block is the body's own block, laid out one level in, and the wrapper's statements take
  the declaration as their origin. One builder serves the method, the lambda and the local function,
  which differed only in where the result was placed. The closure stays: rewriting each `return` to
  return the object instead would read the outs before a `finally` that assigns them runs.
- **The extra frame is the bundler's to place.** The arrow's call is a frame C# does not have. eqc
  marks its line as the declaration's, and Bun's map, after minifying, leads the call's position into
  the arrow's last statement, so the end-to-end test asks about the throw and the caller only.
- **A concise body has one lowering.** A member's expression body was a raw `return` the member writer
  braced, so the expression's arrows were text, and some callers converted inside `InBlock` while
  others did not, which laid a lambda's block out a level off its statement. The member takes
  `ConvertExpressionBodyIr` (a lambda's and a local function's since #384), which converts at the
  block's depth and now knows a body that returns nothing.
- **A creation is a node, and so is an object literal.** `new T(…)` binds as a member access does and
  writes its arguments as a call's; an object literal writes its keys as final text and its values as
  arguments. The strategies are ported branch by branch, each writing what it wrote: a shape that was
  never right and that a map does not need changed (a collection literal with a creation's arguments
  spliced before it) stays text, named as such, for a change of its own. Data (`[TwinIsData]`) stays
  text: it holds numbers and strings, never a lambda.
- **A collection initializer on a node adds per statement.** `(($n) => { $n.add(a); …; return $n; })(new Column())`
  is a block now, each `add` carrying its element, so a breakpoint on a child's line binds; no
  transpiled pin had one.
- **A mapping carries its tree, and the map numbers files.** The path a mapping recorded was never
  written. A tree, rather than a path, is what a map needs from a file: its name, and its text when
  the map carries it, with nothing looked up. Two trees of one path (the parser's and the
  compilation's) are one file.
- **One escape, System.Text.Json's.** Every JSON the build lays out goes through `JsonWriter`, which
  escapes with `JsonEncodedText` and the relaxed encoder: nothing it writes is placed in markup, and
  the default encoder would spell every `<` of a generic argument and every accented letter as an
  escape. Relaxed still escapes every control and writes an unpaired surrogate as U+FFFD. The culture
  catalogs are the exception, and stay on the serializer's default encoder, because the server inlines
  them raw in a script and `<` must not survive there.
- **One map writer.** eqc's map and the composed one carried the same members in two writers, one of
  them by hand; both now go through `SourceMapWriter`, on top of `JsonWriter`.
