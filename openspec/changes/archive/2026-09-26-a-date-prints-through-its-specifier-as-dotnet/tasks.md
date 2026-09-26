# Tasks

## 1. The compiler

- [x] 1.1 Leave a DateTime's ToString to ToStringStrategy, which routes it to the formatter with its provider read through NamedCulture

## 2. The runtime

- [x] 2.1 Write o, s, u and R from a date's own parts, R in the invariant names, and U read as local and moved to UTC
- [x] 2.2 Draw a custom date picture through renderPattern, with f, F, K, % and quoted text
- [x] 2.3 Take the invariant patterns with no culture in force, and keep a year below 100

## 3. Checks

- [x] 3.1 `DateTimeToStringConformanceTests`: every one-letter specifier with no provider, the invariant, the current and a null one, a provider alone, custom pictures, the first year and a null DateTime?; all 10 cases fail on the base
- [x] 3.2 The cross-pinned fixture carries every date specifier the subset admits; 5 of its 8 tests fail on the base
- [x] 3.3 `CultureCrossingTests`: a DateTime's invariant provider stays in the build, and a named one is EQ2108

## 4. Documentation

- [x] 4.1 Add the `docs/LEDGER.md` line citing #388
- [x] 4.2 Update the wiki's SupportedFeatures page, English and Portuguese, on a wiki branch named like this pull request's
- [x] 4.3 Archive this change before the merge
