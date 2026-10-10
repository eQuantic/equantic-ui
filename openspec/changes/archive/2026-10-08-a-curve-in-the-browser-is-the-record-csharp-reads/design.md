# Design

## Context

See proposal.md for what fails and why it matters. The constraints that shape the fix:

- A curve reaches browser code from five producers: the generated design system (its `Curve`
  presets and its `Motion` roles), the runtime itself (`TransitionSpec`'s default easing), code eqc
  emits (`new Curve(…)`, `Curve.Standard.X1`), a hydration payload, where a page's state is JSON,
  and the lowering, which reads a transition's easing into `cubic-bezier()`.
- `Color` set the pattern for a value the browser holds as data (#494): `[TwinIsData]` on the
  vocabulary type, read by symbol at the three points that lower a member of it (an instance method,
  the text, a construction). Equality, `with` and deconstruction already answer through
  `$eq.equals`, `$eq.withPatch` and property reads.
- eqc writes a `float` constant as the double that holds it (`ConstantLiteral`), so
  `new Curve(0.2f, …)` holds `0.20000000298023224` in the browser, and `$eq.equals` compares numbers
  exactly.
- Nothing in the runtime or the component libraries evaluates a curve. A web transition hands it to
  CSS, and only Photon, which draws its own frames, calls `CurveEvaluator`.

## Goals / Non-Goals

**Goals:**

- One shape for a curve in the browser, the record's, read the way C# reads it.
- A build that refuses what cannot run, rather than a page that throws.

**Non-Goals:**

- A twin of `CurveEvaluator`. Nothing in a page asks it today.
- Changing the CSS a transition writes, on either side.
- `SpringSpec`, which nothing consumes yet.

## Decisions

### The curve's twin is its data, as `Color`'s is

Flutter has one `Cubic(a, b, c, d)` everywhere, a const object whose four fields are read by name,
and `Curves.easeInOut` is an instance of it; `docs/FLUTTER-PARITY.md` §4 marks the `Curves` row
SAME. Flutter has no server-to-browser wire, so a class costs it nothing. Here a hydration payload
makes a curve without a constructor, and so does the design system's own export, which already
held the presets as an object of statics under the name `Curve`. So the browser holds a curve as
its data, `[TwinIsData]` says so, and the members a component can call lower to that data through
the rules `Color` already uses. A record of four floats has no method of its own beyond the record's
(`Equals`, `ToString`, `Deconstruct`), and each of them already has a lowering for data.

Alternative considered: a runtime class `Curve` with static presets. Rejected because every producer
above would have to construct it, and the hydration spec would have to rebuild it (`of: Curve`, an
`instanceof` over it) or `Curve.Standard == spec.Easing` would compare a class with a plain object.

### A point is the single C# holds, written as its double

The generator writes `0.2f` as `0.20000000298023224`, as eqc writes the same constant, so a preset
and the curve a component makes from the same points are equal in the browser as in .NET. The CSS
reads the same either way: both lowerings round a number to four places (`TokenCss.Number`, `num`).

### The record text names each member's kind

`$eq.text.record(value, name, members, kinds)` writes each member as an interpolation hole of its
own type writes it, so a float prints the single's digits, `0.2`, where JavaScript's are the
double's. eqc passes a kind only for a member whose text its kind decides; an integer's text is its
digits at any width. A type with no such member passes no kinds at all, so `Color`'s text is emitted
as it was.

### The evaluator stays on the host

#518 offered two answers: fence `CurveEvaluator` as host-only, or give it a twin that evaluates the
record. The web never asks it: a transition is a CSS timing function, and the browser evaluates the
curve itself, as Flutter's engine evaluates a `Cubic` on its own frames. A twin would be a
cubic-bézier solver in TypeScript whose Newton-Raphson steps would have to agree with .NET's
single-precision ones for the conformance suite, written for a caller nothing has. So
`CurveEvaluator` is `[ServerOnly]`, and a call from a component fails the build with EQ2010. When a
browser-side caller appears, a canvas painter easing its own frames, the twin can be added then,
`[RuntimeProvided]` instead of `[ServerOnly]`, and the conformance suite will hold it to the solver.

### The fence asks about the home, not the receiver

`curve.Ease(t)` is `CurveEvaluator.Ease(curve, t)`: the receiver is the first argument, not a type
the member was reached through. The fence was asked with the receiver, saw a type that crosses, and
let the call through, the same way for `EffectiveTypeStyle`'s extensions on a `Text` or a
`TypeStyle`. `ExtensionHome`, which a call and a method group both go through, now asks the fence
about the home's own static (`ReducedFrom`) with no receiver. It sits after the EQ2004 branch, which
must stay first (`BclSurfaceAuditTests`), and before the reduced form a framework home without
`[RuntimeProvided]` keeps. That makes an extension on its receiver the seventh expression way of
naming a host-only symbol that the fence counts, and a type position the eighth.

## Risks / Trade-offs

- [A producer writes an array again] → the vitest pins compare the presets, the motion roles and
  the transition default with the C# values (`primitive-values.fixture.json`), and the conformance
  suite reads them through eqc.
- [A component that called `Ease` stops building] → it threw in the browser before, and the message
  says where the call belongs.
- [A preset holds `0.20000000298023224`] → the CSS rounds to four places on both sides, pinned by
  the S6 cross-pins.

## Migration Plan

None for code that worked. A component that called `CurveEvaluator.Ease` from browser-side code
fails the build with EQ2010: hand the curve to a `TransitionSpec`, or call the evaluator from server
code.
