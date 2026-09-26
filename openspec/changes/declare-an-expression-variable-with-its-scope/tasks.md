# Tasks

## 1. One owner for expression variables

- [x] 1.1 `ExpressionVariableScanner` replaces `PatternVariableScanner`, the method-top hoisting and the primitive Render path's copy
- [x] 1.2 Every statement that holds an expression declares its variables with Roslyn's scope; a while and a for in the head's `let`
- [x] 1.3 Initializers declare in an arrow of their own; a switch expression declares its arms' once
- [x] 1.4 From the author's review: a switch declares its sections' variables for its whole block, a `do` declares its condition's in a per-iteration head, a query's clauses declare their own, and a deconstruction that initializes a `for` joins the head's `let`
- [x] 1.5 Check: `ExpressionVariableConformanceTests` run on both sides; 43 of its first 47 cases fail on main without the harness's own declarations, and 28 with them, and the review's cases fail on the branch before their fixes

## 2. Names and modes

- [x] 2.1 Every declaration site crosses through `ToJsIdentifier`, whose list holds every keyword
- [x] 2.2 Plain JavaScript carries no local annotation; a static property is written static
- [x] 2.3 Check: `ExpressionVariableEmissionTests` run every member kind through `ComponentCompiler` in both modes against .NET; on main neither mode's modules load

## 3. Documentation

- [x] 3.1 The wiki's SupportedFeatures and Compiler pages in English and Portuguese, on the wiki branch named like this pull request's
- [x] 3.2 One `docs/LEDGER.md` line citing #466
- [x] 3.3 Archive this change in the same pull request
