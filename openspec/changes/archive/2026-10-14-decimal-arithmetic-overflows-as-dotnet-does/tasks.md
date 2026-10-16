# Tasks

## 1. Measure before changing

- [x] 1.1 Generate random operations on .NET 10 over operands of every width and scale `DecCalc` takes a path for, and compare the runtime's answers with them
- [x] 1.2 Read .NET 10's `Decimal.DecCalc.cs` for `DecAddSub`, `VarDecMul`, `VarDecDiv`, `VarDecMod`, `ScaleResult`, `SearchScale` and `OverflowUnscale`

## 2. The runtime

- [x] 2.1 Bring `add`, `sub` and `mul` into a decimal as `ScaleResult` does, with the zeros of `VarDecMul`'s two paths
- [x] 2.2 Divide as `VarDecDiv` does: the natural scale, nine places at a time, the rounding, the overflow and the trailing zeros only after a remainder
- [x] 2.3 Keep a remainder's dividend as it is where it is zero or smaller than the divisor
- [x] 2.4 Compare 1.5 million random operations with .NET's answers: none differ
- [x] 2.5 Pin the answers in `decimal.spec.ts` and in `DecimalArithmeticConformanceTests`, executed on both sides; 24 of its 32 cases fail without the change
- [x] 2.6 Run the runtime's suite (`tsc`, `vitest`) and the conformance, compiler and Web suites

## 3. Documentation

- [x] 3.1 The wiki's SupportedFeatures page in the five languages, on the wiki branch of the same name
- [x] 3.2 One ledger entry under `docs/ledger/` citing the issue
