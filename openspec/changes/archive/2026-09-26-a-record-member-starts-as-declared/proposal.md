# Proposal

Closes #385, a Bug under #164 (The transpiler's fences hold on every path).

## Why

A record's instance field initializer went nowhere: the field became a constructor parameter that
defaulted to its type's default, so `record Probe { public string Log = "x"; }` gave `new Probe().Log`
as null. The defaults that did cross were copied as literals into every construction site, so a
literal was all they could be: a property's `= 1.5m` became the number 1.5 and the next decimal
operation threw, a long's `= 5` threw at the first `+ 1L`, a float's `= 0.1f` kept the double, and
`= new()` was null. Measured on main at 52162f3c: 19 of the first 23 cases differed from .NET 10.

## What Changes

- **The twin's constructor writes every member's default**, from the member's own declaration: a
  positional parameter's default, a property's or a field's initializer, converted like any
  expression in the twin's own module. A member with none takes its type's default.
- **A construction that skips a member passes `undefined`**, JavaScript's own omitted argument, and
  the constructor's default runs.
- **An initializer and a base clause read the primary constructor's parameters as its own**, not as
  `this.x`, which is unset where a default runs and unreadable before `super()`.
- **A default imports every struct its zero constructs**: a zero built member by member names a
  struct no syntax of the class does, and four emitters now register it (found in review).

For a developer using the SDK: a record, a struct or a class member starts on the web as its
declaration says, whatever the expression. Nothing is written differently.

The parts reached are eqc and the runtime's transpiled pins (`ChartSeries`, the code languages).
The public surface moves: `ValueMember` loses `Default` (its constructor and `Deconstruct` take three
members), `DefaultValue.Of(ITypeSymbol?)` and `PropertyDefinition.ImplicitDefaultJs` go, and
`CSharpToJsConverter.DefaultOf`, `CSharpToJsConverter.WithConstructorParametersInScope` and
`ConversionContext.ConstructorParametersInScope` arrive. The developer surface does not move. The
migration line: a tool that read `ValueMember.Default` reads the member's declaration instead.

## Capabilities

### New Capabilities

- `transpiler-records`: how eqc builds the twin of a record, a struct or a class: its constructor,
  and what each member starts as.

### Modified Capabilities

None.

## Impact

`src/eQuantic.UI.Compiler` (the record emitter, object creation, identifiers, the default table and
the emitters that ask it), the regenerated pins, and tests in the conformance and compiler suites.
