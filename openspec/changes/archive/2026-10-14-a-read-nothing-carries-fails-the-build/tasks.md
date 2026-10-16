# Tasks

## 1. Measure before changing

- [x] 1.1 Log every read of a .NET property or field that reaches the member-access fallback across the compiler, conformance, Server, Design and Web suites and the samples
- [x] 1.2 Sort the reads by what carries them in the browser: a string's and an array's `length`, an exception's twin, a dictionary's pair, an interface's member, and the culture a char's casing converted as a value

## 2. The compiler

- [x] 2.1 Refuse with EQ2004, in `PlatformMemberRead`, a read of a .NET property or field nothing carries, once per chain and at the in-tree node, and ask it from `MemberAccessStrategy`
- [x] 2.2 Read a char casing's culture from its spelling only, never converting it
- [x] 2.3 Pin both ways in `PlatformMemberReadTests`: the refusals fail without the change, and the culture cases fail without 2.2
- [x] 2.4 Read a nullable tuple's element through `?.`, held against .NET in `NamedTupleConformanceTests`, which fail without it
- [x] 2.5 Hold a decimal's `Scale`, which the runtime's `Decimal` carries, against .NET in `DecimalConformanceTests`
- [x] 2.6 Regenerate the BCL audit's and the diagnostics' baselines and read every verdict that moved against the runtime's twins
- [x] 2.7 Run the compiler, conformance (executed on both sides), Server, Design and Web suites

## 3. Documentation

- [x] 3.1 `docs/DIAGNOSTICS.md`'s EQ2004 row
- [x] 3.2 The wiki's Diagnostics page in English and Portuguese and its SupportedFeatures page in the five languages, on the wiki branch of the same name
- [x] 3.3 One ledger entry under `docs/ledger/` citing the issue
