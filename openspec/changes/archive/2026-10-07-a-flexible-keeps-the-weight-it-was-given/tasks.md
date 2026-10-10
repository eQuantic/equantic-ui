# Tasks

## 1. Measure before changing

- [x] 1.1 Render `Flexible(child, flex: 0, basis: 540)` through the C# realizer and the twin: the server wrote `flex: 1 1 540px` and the twin `flex: 0 1 540px`; a negative weight was clamped to 1 on the server and written as `flex: -3 1 0%` by the twin
- [x] 1.2 Lay the same trees out on Photon: the clamped weight gave 720 and 720 in one line and Cura's 657 and 497 in a wrapping row of 1154, and a zero that reached the engine through an object initializer was dropped from the row
- [x] 1.3 Measure the CSS each side writes in Chrome 154: `flex: 0 1 540px` beside `flex: 1 1 0%` is 540 and 900 at 1440, the wrapping row is 540 and 614, `flex: 0 1 0%` sizes a 200px child at 0 and `flex: 0 1 auto` at 200, and a negative `flex-grow` is dropped

## 2. The weight the app writes is the weight that renders

- [x] 2.1 Check the three numbers on the C# init accessors, refusing a negative weight or shrink and a basis that is negative or not finite (`Flexible.cs`)
- [x] 2.2 Write a zero weight as `flex: 0 <shrink> <basis>`, with `auto` as the basis when there is none, in the C# realizer and the twin's lowering
- [x] 2.3 Refuse the same numbers in the twin after its trailing config, and accept `flex`, `basis` and `shrink` in that config as the C# initializer can set them
- [x] 2.4 On Photon, lay a zero weight out as a rigid item in the single-line pass (a slot of its basis, or its content with the main axis decided by it), let it give space back by its shrink, keep it out of `hasFlexibles`, and measure it from its content in the wrapped pass
- [x] 2.5 Prove it both ways: `FlexibleWeightTests`, `flex-weight.spec.ts`, two cases in the component parity fixture and `FlexZeroWeightLayoutTests` fail against the code before the fix and pass with it

## 3. Proof and documentation

- [x] 3.1 Run the filtered web suites (`FlexibleWeightTests`, `ComponentParityFixtureTests`, `WebRealizerTests`, `SpinnerRealizerTests`, `HeadingOutlineTests`), the runtime's type-check and its flex specs, the native layout tests that hold a Flexible, and the served runtime's budget
- [x] 3.2 `./scripts/public-api.sh update` declares nothing: no signature moved
- [x] 3.3 The wiki's WriteOnceComponents page states the zero weight, and for #684 SupportedFeatures and EmailRealizer are corrected, in English and Portuguese, on the wiki branch named like this pull request's
- [x] 3.4 One `docs/LEDGER.md` line citing #680 and #684
