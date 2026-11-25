# Tasks

## 1. The sum

- [x] 1.1 `utils/overflow.ts`: `addInt` and `addLong`, an addition checked at the type's range with .NET's message; `$eq.num.addInt`, `$eq.num.addLong`
- [x] 1.2 `LinqAccumulation.ValuesOf` reads the values' type and nullability from the bound overload, for `Sum` and `Average` alike; `Add` adds by that type and passes a nullable's null over as its zero
- [x] 1.3 `SumStrategy` adds by the bound overload's type, and keeps the inline reduce for a real
- [x] 1.4 Verify: `AlwaysCheckedConformanceTests` runs int and long sums, a running total that leaves the range and comes back, a selector of each kind, the nullable forms and the reals on both sides; against main they fail, and so do a check of the final total only and a sum that adds a nullable's null

## 2. The magnitude

- [x] 2.1 `utils/overflow.ts`: `abs`, `absLong`, `copySign` and `copySignLong`, throwing for a signed width's MinValue with .NET's message, and an integer's one zero for `CopySign(0, negative)`; `$eq.num`
- [x] 2.2 `PrimitiveStaticStrategy` writes a signed integer's `Abs` and `CopySign` through them, for `Math` and the type alike, and leaves a real's to `Math.abs`
- [x] 2.3 Verify: the conformance cases run `Abs` and `CopySign` at MinValue for every signed width, in an unchecked context, and the magnitude comparisons that must not throw; against main they fail, and so do `checked(Math.abs(x))` (the wrong message) and a `CopySign` that answers `-0`

## 3. The documentation

- [x] 3.1 The wiki's Compiler page, in its five languages, says which members check whatever the context
- [x] 3.2 One ledger entry under `docs/ledger/`, citing #1808
