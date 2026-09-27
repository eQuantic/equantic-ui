# Tasks

## 1. The writer

- [x] 1.1 Carry an arrow's block as a `JsStatement` in its own node, `JsArrowBlock`, with the layout and depth it is laid out at
- [x] 1.2 Share the statement writer's builder, compose every expression node's marks through it, and lay a block out once per arrow
- [x] 1.3 Place each expression a statement holds with its marks, and write a statement that reads the same in either layout in one place
- [x] 1.4 Check: the writer's own tests place an arrow's block in a call, a template hole and a bound template part, and mark nothing in the compact layout; marking changes nothing the writer writes

## 2. The strategies

- [x] 2.1 Hand the writer the lambda's and the `delegate`'s block, and lower a concise body once, for a lambda and a local function
- [x] 2.2 Bind a local function as a `const` to a block arrow, retire `JsConstArrow`, and convert a switch section at its statements' depth
- [x] 2.3 Build `List`'s own method calls and collection expressions as IR, and take both strategies off the text baseline
- [x] 2.4 Check: `StatementSourceMapTests` reads 13 lines inside lambda blocks (a `List.ForEach`, a method of the app's own, a `Where`, a `Select`, a local's, a `delegate`, an arrow in an arrow, an async one, a concise body's added block), all 13 without a segment on main at a4112476, and `StackFrameSourceMapTests` throws inside a `List.ForEach` block and reads the frame through the composed map, which led to the line that holds the lambda on main; every transpiled pin holds

## 3. Review

- [x] 3.1 Resume the statement after a block's closing brace, and keep a braced body's origin
- [x] 3.2 Split the block arrow from the expression arrow, with no default layout
- [x] 3.3 Write a List call's text only where its shape still needs it
- [x] 3.4 Check: a frame thrown by what follows a block (`… }).Count + Check(n)`) leads to the statement through the composed map, which it did on main and not before this fix; the braced body's line maps to itself, where it mapped to the `if`

## 4. The measure

- [x] 4.1 Count, per shared source, the statements inside a lambda's block with no segment of their own, against a baseline that may only shrink
- [x] 4.2 Check: the baseline holds 59 on this branch merged with 0.2.0-preview.59, where main holds 79 for the same sources

## 5. Documentation

- [x] 5.1 One `docs/LEDGER.md` line citing #384
- [x] 5.2 The wiki's Debug, Roadmap and ServerIntegration pages, English and Portuguese, on a wiki branch named like this pull request's, saying what maps and what maps to its statement for now
- [x] 5.3 File what this found outside its scope: #487, #488, #490, #491, and the two carriers the baseline still counts (#492)
- [x] 5.4 Check: `./scripts/check-openspec.sh` validates the change strictly
