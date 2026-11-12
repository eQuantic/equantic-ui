## Why

Closes #824 (under #565), #890 (under #849) and #934 (under #845). Each is a module that names something other than what it writes or imports.

- A page that reads a resource string imported `./Strings`, a module nothing writes (#824). The project's scan took the resx Designer class for a plain class, which is a module, while the parser knows it is rewritten to `$eq.str` at every read and writes none. And the test of whether the emitted text names a type matched identifier tokens over the whole text, string literals included, so the catalogue's id in `$eq.str('Strings', 'Hero.Title')` counted as a reference, as did a constant inlined as `'Copy'` for the class `Copy`. Since #624 an import is what compiles a referenced project's type, so a word in a string compiled a type nothing used.
- A file that called both a class of the solution named `Math` and `System.Math` wrote both as `Math`, and a module has one binding of a name: `Math.twice(2) + Math.max(1, 2)` under an import of the class, so `Math.max` was not a function (#890). The same holds for every global the emitted code reads (`Number`, `Symbol`, `Set`, `String` and the rest), in the class's own module too.
- A type named by a word JavaScript reserves was written as `export class function`, which no module parses (#934). `function` needs no escape in C#, and `@class` is the escape C# offers for its own keywords.

## What Changes

- A type's twin takes a `$` after its name when that name is a word JavaScript reserves or a global the emitted code reads (`function$`, `class$`, `Math$`), its module too, as a local of that name already did. The rule is `TwinName.OfNested`, shared by eqc and the server, with the two lists moved beside it into one shared `JsNames`. A type of .NET's or of the vocabulary keeps its name, and a nested type is named by its owner already.
- Every site that introduces a type's twin writes the name it registers for the import: a type read through its namespace (`App.Util.Math.Family()`), an extension's home, an extension property's and indexer's, a construction through an alias, and a local that starts null, whose annotation was mapped from its text and named `Item` for `Cart.Item` too.
- The scan asks of a class's symbol whether it is a resource class, as the parser does, and is no module of it. The test of whether a module's text names a type reads outside its string literals, the component path's as the record path's already did.

## Capabilities

### Modified Capabilities

- `module-imports`: a class named `Math` is imported as `Math$`, and a name inside a string is no reference.
- `transpiler-names`: a type named by a reserved word or a global takes a `$`.

## Impact

- Reaches `eQuantic.UI.Compiler` (`TypeSymbolExtensions`, `StringExtensions`, the strategies that introduce a twin, `LocalDeclarationStrategy`, `NamespaceRemovalStrategy`, `TypeScriptEmitter`, `ComponentParser`, `PlainClassModule`), the shared `TwinName` and the new `JsNames` (linked into the Compiler, the Generators and the Server). A component's action keeps the id #1290 gave it, its CLR full name, whatever its twin is named.
- An app with a class named like a global or a reserved word gets a module named `Math$.js` or `function$.js` where it got `Math.js`, and a page of that name is served by it.
- Neither `PublicAPI.Shipped.txt` / `PublicAPI.Unshipped.txt` nor `developer-surface.baseline.txt` moves.
