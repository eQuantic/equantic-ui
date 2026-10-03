# Tasks

## 1. An enum's text is .NET's

- [x] 1.1 Write a flags enum's, a nullable enum's and a concatenated enum's text through the runtime's `text`, and a member named in the source as its name. Verify: `EnumConformanceTests.AnEnumsText_IsDotNets` runs every shape on both sides
- [x] 1.2 Honour `ToString(format)` and an interpolation's format and alignment. Verify: the `D`, `X`, `F`, `G` and alignment cases of `AnEnumsText_IsDotNets`, and `enums.spec.ts`

## 2. The statics of Enum read the enum's shape

- [x] 2.1 Lower `Parse`, `TryParse`, `GetNames`, `GetValues` and `IsDefined` to `$eq.enums` with the shape inline, by the parameter each argument binds. Verify: `EnumConformanceTests.EnumsStatics_ReadTheEnumsShape` on both sides, and `EnumMethodStrategyTests` pins the spelling
- [x] 2.2 Cast an object holding the enum, and one enum to another, and hold a value no member names as its number. Verify: the cast cases of `EnumsStatics_ReadTheEnumsShape`, and `EnumCastStrategyTests`

## 3. One member table

- [x] 3.1 Make `EnumShape` the one owner of the members, read by the casts, the arithmetic, the ordering and the text. Verify: the compiler, web and conformance suites, whose pins move only where the text now goes through the runtime

## 4. An enum crosses as a dictionary key

- [x] 4.1 Write and read an enum key as the browser holds it, a flags one as its number, and refuse a name no member has. Verify: `EqJsonTests`, and `HydrationCrossingTests.ADictionaryKeyedByAnEnum_CrossesAsTheBrowserHoldsItsKeys` draws the server's text in the browser

## 5. Documentation

- [x] 5.1 The wiki's Transpiler page in English and Portuguese, on the pull request's wiki branch, and one `docs/LEDGER.md` line citing the issues
