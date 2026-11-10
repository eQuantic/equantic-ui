## Why

Closes #1303, #1304, #1305, #1306, #1307 and #1347, Bugs under #1174. The first five were found by the element
engine (#1127) on the 1.0 critical path, and the sixth by the fixtures this change adds.

A class's TypeScript twin failed the runtime's `tsc` in six shapes of ordinary C#, so a module transpiled into the
runtime could not be committed, and an app's own module failed the type check its build runs:

- an abstract member was never declared, so every call the base made to it was "Property does not exist" (#1304);
- a downcast wrote the operand alone, so a member only the target has did not exist on the operand's type, and a
  class compiled with no scan of the app imported none of the app types it named besides its base (#1303);
- a parameter an alternate constructor may leave missing was optional, so storing it into a member was refused (#1305);
- a lambda stored in a static had nothing to type its parameters from (#1306);
- the local a derived type evaluates an initializer into before its base's constructor was an untyped `[]` (#1307);
- a primary constructor's reference parameter defaulted to a bare `null` (#1347).

## What Changes

For a developer using the SDK, nothing they write changes, and nothing the browser runs changes: every fix is
type-only, and the JavaScript is the same. What changes is that the C# above compiles to TypeScript the runtime's
strict `tsc` takes:

- **An abstract method is declared**: `abstract draw(): number;`, whatever its accessibility, its parameters typed as
  an override's are, an optional one marked optional, a task's return declared as the promise an `async` override
  answers.
- **A downcast keeps its type**: `(shape as Square).side`, the target named by its twin and imported. A generic target
  and one the build keeps on the server are written as before. The assertion does not check: a wrong downcast reads
  what the object holds, as it did, where .NET throws `InvalidCastException`.
- **A class module imports what it names** when its compile was handed no scan of the app (a snippet, the playground,
  the runtime's own transpiled modules): it asks the same rules of the compilation in hand, as a record's module
  already did, for a name the emitted text names.
- **A constructor overload's parameters hold their declared type**: `step: F = undefined!`.
- **A static lambda satisfies its static's type**: `$slots.byDepth = ((a, b) => …) satisfies (x: T, y: T) => number`.
- **A hoisted initializer is declared as its member**: `const $$_items: (string | null)[] = [];`.
- **A primary constructor's null default is said to be one**: `name: string = null!`.

Parts reached: eqc only (`TypeScriptEmitter`, `CastExpressionStrategy`, `TwinConstructor`, `TypeInitializer`, the
IR's `JsConst`). The public surface of `eQuantic.UI.Compiler`, a tool, moves: `JsConst` carries an `Annotation` and
`JsStatement.Const` takes one. No contract assembly and no developer surface moves.

## Capabilities

### New Capabilities

(none)

### Modified Capabilities

- `transpiler-classes`: one new requirement, a class's twin passes the runtime's type check.

## Impact

`tests/eQuantic.UI.Web.Tests/SharedComponentTranspilationTests` gains one source of emission shapes, transpiled into
`src/eQuantic.UI.Runtime/src/shared/__fixtures__/Emitted*.ts`, which the runtime's `tsc` checks in CI's test-runtime
job: each shape fails that check on main. Two of the runtime's library modules (`CSharpLanguage`,
`TypeScriptLanguage`) gain type annotations on their hoisted locals, which `tsc` checks too.
