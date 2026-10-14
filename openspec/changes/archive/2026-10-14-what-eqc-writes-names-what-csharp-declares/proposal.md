# Proposal

Closes #704, #740 and #758, Bugs under #565 (The transpiler's fences hold on every path, continued),
and #703, a Bug under #208 (The audit continues).

## Why

Four places where what eqc writes did not name what C# declares:

- **A name spelled verbatim (#704).** eqc read a declaration's name by its spelling, so
  `public static class @Shelf` went out as `export class @Shelf` in a module named `@Shelf`, neither
  of them JavaScript, while every reference wrote `Shelf`. A record so declared printed
  `@Point { X = 1 }`, a `new Store.@Failure()` imported nothing, and a property or a parameter typed
  `@Point` was annotated `@Point`, which no TypeScript parses.
- **A member a derived class declares (#740).** A field moves to a slot of its own beside a member a
  case apart (#396, #696), but only for a member of its own type or its bases. A class derived from it
  that declares `Amount` beside the base's `amount` put a getter on its prototype that the base's own
  field hid: `s.Amount` answered the field's 1 where C# answers 99.
- **A lambda over a long or a decimal (#758).** Its parameter was annotated `number`, where a long is
  a `bigint` and a decimal the runtime's `Decimal`, so a type-checked module (the shared library the
  runtime's `tsc` reads) failed on the first one.
- **A component the generated file cannot reach (#703).** The factory generator read a component's
  own accessibility, so a public component nested in a private or an internal class got a public
  factory that did not compile (CS0122, CS0050), in `AppUI.g.cs`, a file the developer never wrote.
  One inside a `[ServerOnly]` class got a factory too, though it has no module in the browser.

## What Changes

For a developer:

- A name written with C#'s `@` is the name without it, wherever eqc writes it: a type and its module,
  an import, an annotation, a namespace, a member, a parameter and a local.
- A plain class's field moves aside for a member any class derived from it declares a case apart,
  however far down, in the field's own project. A member an app declares a case apart from a field it
  inherits from a class of another project fails the build with EQ1007, naming the field: that
  project's build never reads the app, so its field would hide the member.
- A lambda's parameter is annotated `bigint` over a `long` or a `ulong` and `Decimal` over a
  `decimal`, and the module imports `Decimal`.
- A component gets a factory when it and every class it is declared in are public and none is
  `[ServerOnly]`.

eqc and the factory generator change. The runtime, the realizers, the shells and the SDKs are
untouched, and neither the public surface nor the developer surface moves. Nothing breaks: the
generated code that changes did not compile or did not run, and the new EQ1007 refusal is a member
the browser answered wrongly.

## Capabilities

### New Capabilities

- `component-factories`: which components the generated declarative surface writes a factory for.

### Modified Capabilities

- `transpiler-names`: a verbatim name is written without its escape on every declaration, a type's
  included, and so are its module, its imports and its annotations.
- `transpiler-classes`: a field moves aside for a member a derived class declares, and a member
  landing on a field another project holds is refused.
- `transpiler-expressions`: a lambda's parameter is annotated as a member of its type is.

## Impact

- `src/eQuantic.UI.Compiler`: every name read from syntax is the name's value
  (`Identifier.ValueText`, and `ValueText()` for a type's syntax, `CodeGen/Extensions/ValueTextExtensions.cs`);
  `FieldSlotExtensions` reads the types derived from a field's class; `OverloadedMethods` refuses a
  member that lands on another project's field; `LambdaExpressionStrategy` annotates a long and a
  decimal.
- `src/eQuantic.UI.Generators/FactorySurface.cs`: a factory for a component that crosses.
- `tests/eQuantic.UI.Compiler.Tests/Coverage/NoNameReadAsSpelledTests.cs`: refuses
  `Identifier.Text` in the SDK's source and `ToString()` on a type's syntax in the compiler, asked of
  the model.
- Conformance, referenced-project, generator and shared-library tests pin each case, the last through
  the runtime's own `tsc`.
