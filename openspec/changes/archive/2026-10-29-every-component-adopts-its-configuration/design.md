## Context

`ComponentElement.Update` keeps the instance its position mounted and calls `retained.AdoptConfig(next)` with
the fresh instance the parent's build made. The default copies nothing, so a component lists what it copies,
and a setting missing from the list works on the first build and ignores every later one. Nothing failed
when a list fell behind: a test that builds a component once never meets the condition, and the doc on
`StatefulComponent` records that three components of one consumer app shipped the bug before the rule was
written there.

## How Flutter solves it

Flutter splits the two halves (`docs/FLUTTER-PARITY.md`, the `StatefulWidget` row): the `State` persists, the
`Widget` is the configuration, and the framework swaps `State.widget` for the fresh one before
`didUpdateWidget`. The `State` reads its settings from `widget`, so there is no copy to forget. Here one class
holds both, and `AdoptConfig` is the hand-written copy. The components that keep the fresh instance whole
(`_config = fresh`, then `_config.X` in `Build`) are Flutter's shape done by hand, and every setting added to
them later is carried without a line.

## Goals / Non-Goals

**Goals:** every shipped component carries every input a parent hands it, and a test fails, naming the
component and the input, the day one does not.

**Non-Goals:** the app's own components, which this test does not build; making the element hand a
component its latest configuration itself, as Flutter's `State.widget`, which would retire the hand-written
copy for apps too and is a design of its own; a `Build` that reads `this.X` in a component that keeps its
configuration whole, which a measurement of the instance cannot see.

## Decisions

- **A measurement, not a reading of the source.** A Roslyn pass over the override would count an assignment;
  it would count `Crossed = fresh.Carried` too, and it would have to know which field an accessor writes.
  Handing a second configuration measures the condition itself: the kept instance is asked afterwards.
- **One input at a time.** Each input is varied alone from two instances built alike, so a failure names the
  input and not the component. A public `set` or `init` property is read back from the kept instance. A
  constructor argument is read off what it changes: the public properties first, and the fields only when it
  changes none, since a field beside a property is often state the argument only seeds.
- **The values come from `FactoryArguments`**, which the factory conformance test already builds and varies
  values with. It learns two shapes here: a list (empty becomes one element, one element becomes empty) and an
  abstract slot holding a value (another instance of the leaf it is built with). An input it cannot vary
  fails the test by name, never skipped.
- **Derived, never listed.** The components are every concrete `StatefulComponent` in the `eQuantic.*`
  assemblies beside the test that reference the vocabulary, and a second test asserts the scan reaches the
  three libraries that ship one today.
- **The baseline only shrinks.** An entry is written by hand with its reason. `EQ_UPDATE_ADOPT_CONFIG_BASELINE=1`
  removes the entries that are carried now and never adds one, so it cannot turn a new finding green.
- **`init` stays `init`.** An `init` accessor cannot be written from `AdoptConfig`, and turning it into `set`
  would open a kept instance to writes from outside. The setting is backed by a field instead, the shape
  `ProgressBar` already has.

- **The field a struct setting is backed by is written as its property was.** `SizeValue` has a hand-written
  twin that does not build its zero, so eqc's default for it is `undefined`, the twin's own default. A
  property of it was always left alone (`ImplicitDefault`), and a field was written `= undefined`, which
  `tsc` refuses. The emitter now leaves the field alone too, declared definitely assigned, so `ListView`'s
  `Width` reads in the browser exactly what it read as an auto-property.

## Risks / Trade-offs

- A component that keeps its configuration whole is carried by construction, and the test says so without
  measuring its inputs one by one: a `Build` that reads the kept instance's own stale property instead of
  `_config`'s is not caught.
