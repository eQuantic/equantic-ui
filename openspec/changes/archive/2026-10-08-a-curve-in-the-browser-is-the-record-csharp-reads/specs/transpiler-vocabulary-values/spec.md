## ADDED Requirements

### Requirement: A curve is the record C# reads, wherever the browser holds one

A `Curve` in browser-side code SHALL be the record's data, `{ x1, y1, x2, y2 }`, each point the
single C# holds, whether it is a preset, a motion role's curve, a transition's easing, a curve a
component makes or a curve a page's state carries. Its points, equality, text, copy and
deconstruction SHALL answer as they do in .NET, and a transition's easing SHALL write the same
`cubic-bezier()` on the server and in the browser.

#### Scenario: A preset's point

- **WHEN** a component's browser-side code evaluates `Curve.Standard.X1`
- **THEN** it answers `0.2`, as in .NET, where it read `undefined`

#### Scenario: A motion role's curve

- **WHEN** it evaluates `Motion.Press.Curve.X1`
- **THEN** it answers `0.2`, as in .NET

#### Scenario: A curve a component makes

- **WHEN** it evaluates `new Curve(0.2f, 0.9f, 0.3f, 1.25f).Y2`
- **THEN** it answers `1.25`, where the construction threw "is not a constructor"

#### Scenario: A preset equals the same curve made in code

- **WHEN** it evaluates `new Curve(0.2f, 0f, 0f, 1f) == Curve.Standard`
- **THEN** it answers `true`, as in .NET

#### Scenario: The text

- **WHEN** it evaluates `Curve.Standard.ToString()` and `$"{Curve.Accelerate}"`
- **THEN** they are `Curve { X1 = 0.2, Y1 = 0, X2 = 0, Y2 = 1 }` and
  `Curve { X1 = 0.3, Y1 = 0, X2 = 1, Y2 = 1 }`, as in .NET

#### Scenario: A transition's easing

- **WHEN** a box's style is `new TransitionSpec(StyleChannels.Opacity, 150) { Easing = new Curve(0.2f, 0.9f, 0.3f, 1.25f) }`
- **THEN** the server and the browser both write `opacity 150ms cubic-bezier(0.2, 0.9, 0.3, 1.25)`

#### Scenario: A curve in a page's state

- **WHEN** a page holds a `Curve` field that crosses in its hydration payload
- **THEN** the payload's spec describes it by its four single members and names no class to build
  it on

### Requirement: An extension whose home stays on the host fails the build

A call or a method group of an extension method whose home, or the method itself, is `[ServerOnly]`
SHALL fail the build with EQ2010 on whatever receiver it is reached, and SHALL name no import of the
home. The value it is reached on SHALL still cross.

#### Scenario: The curve's evaluator on its receiver

- **WHEN** a component evaluates `Curve.Standard.Ease(0.5f)`
- **THEN** the build fails with EQ2010 naming `CurveEvaluator.Ease`, where it built and threw in the
  browser

#### Scenario: A delegate of the evaluator

- **WHEN** a component makes `Func<Curve, float, float> ease = CurveEvaluator.Ease`
- **THEN** the build fails with EQ2010

#### Scenario: Another host-only home on a value that crosses

- **WHEN** a component evaluates `new Text("hi").Resolve(theme)`, an extension of `EffectiveTypeStyle`
- **THEN** the build fails with EQ2010

#### Scenario: The curve still crosses

- **WHEN** a component reads `Curve.Standard.X1` or makes `new Curve(0.2f, 0.9f, 0.3f, 1.25f)`
- **THEN** the build reports nothing: only the evaluator stays on the host

### Requirement: A record member of a value the browser holds as data is the delegate its call is

`Equals`, `ToString` and `GetHashCode` of a vocabulary value type the browser holds as plain data,
made into a delegate, SHALL answer what their calls answer, through the helpers the calls use, on a
`Color` as on a `Curve`. The delegate SHALL read its receiver once, when it is made, as C# copies the
receiver into the delegate.

#### Scenario: Equals as a delegate

- **WHEN** a component's browser-side code makes `Func<Curve, bool> same = Curve.Standard.Equals` and
  calls `same(new Curve(0.2f, 0f, 0f, 1f))`
- **THEN** it answers `true`, as in .NET, where making the delegate threw

#### Scenario: ToString as a delegate

- **WHEN** it makes `Func<string> text = Color.FromRgba(1, 2, 3, 4).ToString` and calls it
- **THEN** it answers `Color { R = 1, G = 2, B = 3, A = 4 }`, as in .NET, where it answered
  `[object Object]`

#### Scenario: A curve's text as a delegate keeps its singles

- **WHEN** it makes `Func<string> text = Curve.Standard.ToString` and calls it
- **THEN** it answers `Curve { X1 = 0.2, Y1 = 0, X2 = 0, Y2 = 1 }`, as in .NET

#### Scenario: The receiver is read once

- **WHEN** it makes `Func<string> text = Make().ToString`, where `Make` counts its runs, and calls the
  delegate twice
- **THEN** `Make` has run once, as in .NET

#### Scenario: The value the delegate was made with

- **WHEN** it makes `Func<string> text = c.ToString` over a local `c` holding `Curve.Standard`, assigns
  `Curve.Decelerate` to `c`, and calls the delegate
- **THEN** it answers the standard curve's text, as in .NET
