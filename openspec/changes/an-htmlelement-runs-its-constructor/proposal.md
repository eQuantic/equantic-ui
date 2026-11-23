## Why

Closes #1837, a Bug under #1587.

A class that derives from `HtmlElement`, the DOM escape hatch, and takes constructor parameters reached the browser
without what its constructor does. `new Chip("a", 5).Says()` read `a|5` in .NET and `|` in the browser. The primitive
path's parser kept a constructor reader of its own beside the component path's `ParseConstructors`, and that reader
stored no constructor's syntax:

- no body ran, a block's or an arrow's;
- the emitter stored every parameter under its own name instead, a member nothing read (`this.label` beside a body
  writing `_label`);
- no parameter kept its C# default;
- a primary constructor was not read at all, so the twin took `props` and the parameters were gone.

Two more mechanisms of the same path showed in the same measurement and are other issues, both williancesar's: an
expression-bodied `Render()`, written as the fallback `div` (#1540), and an instance field's initializer (#1541).

## What Changes

For a developer using the SDK: an `HtmlElement` subclass behaves in the browser as its constructor says.

- `new Chip("a", 5)` runs the constructor's body.
- `new Chip("b")` gets the parameter's default.
- `class Tag(string label) : HtmlElement` holds `label`.
- An initializer beside the arguments is applied after them, as before.

- **One constructor reader.** `ParsePrimitiveClass` reads constructors through `ParseConstructors`, the component
  path's, and its own reader is gone.
- **Shared constructor writers.** The emitter's primitive branch writes its constructor through three writers it now
  shares with the component branch:
  - `ConstructorParameter`, a parameter with its default;
  - `ParameterStores`, what a primary constructor stores, or an explicit one where its body leaves a property to it;
  - `ConstructorBody`, the block or the arrow.
- **An interface parameter stays a parameter.** A construction site passes it as written, so the twin takes it as
  written.

Parts reached: eqc only (`ComponentParser`, `TypeScriptEmitter`). No public surface and no developer surface moves;
the shared components the runtime ships are unchanged byte for byte.

This is a step in the structural direction written into #1837: `HtmlElement` subclasses on the component path's member
parsers and emitter branch, with `render()` in place of `build(context)`, which would close #1540 and #1541 too. It
takes the constructor's part of that direction and leaves those two alone.

Rules of `docs/PRINCIPLES.md` followed: one reader, one writer per construct. The primitive path's second constructor
reader is gone, and its constructor is written by the component branch's writers.

## Capabilities

### New Capabilities

(none)

### Modified Capabilities

- `transpiler-classes`: one new requirement, an `HtmlElement` subclass runs its constructor as C# does.

## Impact

`tests/eQuantic.UI.Conformance.Tests` gains `HtmlElementConstructorConformanceTests`, nine shapes read on both sides in
both modes, eight of which diverge on main.
