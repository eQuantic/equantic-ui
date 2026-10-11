# Tasks

## 1. Measure before changing

- [x] 1.1 List every Spacer the tree builds: the factory's default, the literal weights 1 (the samples and the tests), 360 (the spinner tests) and 700 (ProgressBar's indeterminate segment), `Spacer.Fixed` and `Gap` for the rigid form, and ProgressBar's counterweight `1000 - filledWeight`, added only while the fill is below 1000. None is below 1
- [x] 1.2 Render a fractional basis on each side: 540.125 is `540.13px` on the server and `540.125px` in the twin, and the float 540.12 reaches the twin as 540.1199951171875 and is written whole; `px`, which every other length goes through, writes `540.13px` and `540.12px`

## 2. The numbers render as written

- [x] 2.1 Refuse a Spacer weight below 1 on its C# init accessor, over an explicit field the rigid form writes past (`Spacer.cs`), and in the twin after its trailing config (`vocabulary.ts`)
- [x] 2.2 Write the twin's basis through `px` (`lowering.ts`)
- [x] 2.3 Prove it both ways: `SpacerWeightTests`, the basis and Spacer cases of `flex-weight.spec.ts` and the `flexible-fractional-basis` case of the component parity fixture fail against the code before the fix and pass with it

## 3. Proof and documentation

- [x] 3.1 Run the filtered web suites that build a Flexible or a Spacer, the runtime's type-check and its specs, the native layout tests that hold a Flexible or a Spacer, and the served runtime's budget
- [x] 3.2 The wiki, in English and Portuguese, on the wiki branch of this pull request: EmailRealizer's example builds the bulletproof button as a Link around a Box and its Track M sentence says a Button is refused (#694), measured through `EmailRenderer` and compiled whole by `WikiClaimsCompile`; WriteOnceComponents says a Spacer's weight is 1 or more (#691); and the wiki guards run against the branch
- [x] 3.3 `FlexNode.Wrap`'s doc comment says how a wrapping container breaks and resolves its lines, and the audit's citations into `lowering.ts` and the web realizer that were stale point at the lines they name
- [x] 3.4 The `docs/LEDGER.md` line of this pull request cites #691, #692 and #694
