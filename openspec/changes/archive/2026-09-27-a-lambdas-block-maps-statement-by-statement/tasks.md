# Tasks

## 1. The writer

- [x] 1.1 Carry an arrow's block as a `JsStatement`, with the layout and depth it is laid out at
- [x] 1.2 Share the statement writer's builder, and compose every expression node's marks through it
- [x] 1.3 Place each expression a statement holds with its marks
- [x] 1.4 Hand the writer the lambda's and the `delegate`'s block, and map a concise body's added block to the body

## 2. List

- [x] 2.1 Build `List`'s own method calls as IR, and take `ListMethodStrategy` off the text baseline

## 3. Checks

- [x] 3.1 `StatementSourceMapTests`: 13 lines inside lambda blocks (a `List.ForEach`, a method of the app's own, a `Where`, a `Select`, a local's, a `delegate`, an arrow in an arrow, an async one, a concise body's added block), all 13 without a segment on main at a4112476
- [x] 3.2 `StackFrameSourceMapTests`: a frame thrown inside a `List.ForEach` block leads to its statement through the composed map, where main led to the line that holds the lambda
- [x] 3.3 The writer's own tests: an arrow's block in a call, a template hole and a bound template part, and none in the compact layout
- [x] 3.4 Every transpiled pin holds, and the public API declares the five retired signatures

## 4. Documentation

- [x] 4.1 One `docs/LEDGER.md` line citing #384
- [x] 4.2 The wiki's Debug, Roadmap and ServerIntegration pages, English and Portuguese, on a wiki branch named like this pull request's
- [x] 4.3 File what this found outside its scope: #487, #488
