# Proposal

Closes #518, a Bug under #164 (The transpiler's fences hold on every path, under the epic #157).

## Why

`Curve` is a positional record struct in C#, `Curve(float X1, float Y1, float X2, float Y2)`, with
the presets `Standard`, `Decelerate` and `Accelerate`, and a `MotionSpec` pairs one with a duration.
In the browser a curve was two other shapes, and neither was the record. The generated design system
exported a preset as an array, `standard: [0.2, 0, 0, 1]`, and the runtime's `MotionSpec` declared
its curve a preset name. So browser code that read a curve the way C# does met neither:
`Curve.Standard.X1` and `Motion.Press.Curve.X1` read `undefined`, and `new Curve(…)` was emitted
against an object that is no constructor. Falei.pt met it on 0.2.0-preview.60, with
`new Curve(0.2f, 0.9f, 0.3f, 1.25f)` as a transition's easing: the page built with no diagnostic and
threw `TypeError: Curve2 is not a constructor` in the browser.

Copilot found one more shape of it on #656: `CurveEvaluator.Ease(this Curve curve, float t)` called
from browser code. Its home has no `[RuntimeProvided]`, so the call kept the reduced form,
`curve.ease(t)`, and a method group bound `curve.ease.bind(curve)`. Neither works in the browser,
and both built with no diagnostic. The fence that refuses a host-only symbol was asked about the
type the call was reached through, the receiver, and a curve crosses, so the fence waved it on.

The export check #494 added, `EveryValueTypeOfTheVocabulary_IsDataInTheBrowser_ExactlyWhenItSaysSo`,
listed `Curve` as its one exception, with this issue's number.

## What Changes

- **A curve is the record's data in the browser, wherever it meets one.** A preset, a motion role's
  curve, a transition's easing, a curve a component makes and a curve in a page's state are all
  `{ x1, y1, x2, y2 }`, each point the single C# holds. `Curve` carries `[TwinIsData]`, as `Color`
  does, so eqc builds a construction as the data, writes the text as .NET's record text, and
  compares, copies and deconstructs a curve as it does a colour.
- **The design system writes that shape.** The generator writes each preset as the record's data,
  every point the double that holds its single (`0.2f` is `0.20000000298023224`), so a preset equals
  the curve a component makes from the same points. A motion role is the runtime's `MotionSpec`
  holding the preset it names, `TransitionSpec.of` takes a `MotionSpec`, and the lowering reads the
  record's points into `cubic-bezier()`. The CSS does not change.
- **A record's text writes a float member as a single.** eqc passes each member's number kind to the
  runtime's record text, so `$"{curve}"` prints `X1 = 0.2`, as .NET does, where JavaScript's own
  digits are the double's.
- **The evaluator stays on the host.** `CurveEvaluator` is `[ServerOnly]`: a web transition is a CSS
  timing function, and the browser evaluates the curve itself. A component that calls
  `curve.Ease(t)` or `CurveEvaluator.Ease(curve, t)`, or makes a delegate of either, fails the build
  with EQ2010.
- **An extension whose home is host-only is refused on its receiver.** The fence asks about the
  extension's home, never about the receiver, which is only its first argument. The same hole let
  `typeStyle.WithCodeFace(theme)` and `text.Resolve(theme)` through, over homes fenced before.
- The exception for `Curve` in the export check goes, and the list that held it with it.

For a developer: the C# they write stays the same, and it runs in the browser now. A component that
called `Ease` from its browser-side code built and threw before; it fails the build now.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `transpiler-vocabulary-values`: a curve is the record C# reads wherever the browser holds one, and
  an extension of a vocabulary value whose home stays on the host fails the build.

## Impact

- **eqc**: the host-only fence on an extension's home (`InvocationStrategy.ExtensionHome`), and the
  record text of a data twin (`StringConversion`).
- **The runtime**: `CurveValue`, the lowering of an easing, `TransitionSpec`, `MotionSpec`,
  `recordText`, and the generated design system.
- **`eQuantic.UI.Primitives`**: `[TwinIsData]` on `Curve` and `[ServerOnly]` on `CurveEvaluator`.
  No signature moves, so `PublicAPI.*.txt` does not, and neither does the developer surface.
- **`eQuantic.UI.Web.Build`**: the design-system generator.
- **Not reached**: the web realizer, whose `TokenCss.Bezier` already reads the record, the Photon
  shells, which evaluate a curve with `CurveEvaluator` in .NET, the SDKs and the templates.
- **Break**: a component that calls `CurveEvaluator.Ease` from browser-side code fails the build with
  EQ2010, where it threw in the browser. Migration: hand the curve to a `TransitionSpec`, which the
  browser evaluates, or call the evaluator from server code.
