# Tasks

## 1. The wrap

- [x] 1.1 `IntegerWidth`: every width wraps in the default context. `Arithmetic` handles `+ - * <<`: an int product goes through `Math.imul`, and an int's `<<` is JavaScript's own. `Quotient` handles `/` and `%`. The 32-bit wraps are operators the writer places.
- [x] 1.2 `ArithmeticContext.ProvedInRange` replaces `ExplicitUnchecked`: a constant C#'s compiler folded outside `unchecked` is left as written.
- [x] 1.3 Every call site moves to the new rules: binary, lifted, unary step and negation, compound assignment, and enum arithmetic.
- [x] 1.4 A postfix step that a void lambda holds is the assignment.

## 2. Proof

- [x] 2.1 Conformance: default-context overflow of int, long and ulong, on every operator, on both sides. These cases fail without the change.
- [x] 2.2 The generated differential fold overflows an int and a long on purpose, and fails without the change.
- [x] 2.3 The conversion gaps baseline loses its two overflow lines.
- [x] 2.4 Regenerate the emission pins and the transpiled shared components, and measure the served runtime.

## 3. Documentation

- [x] 3.1 Update the wiki's Compiler and SupportedFeatures pages in all five languages, CLAUDE.md and the coverage documents.
- [x] 3.2 Add a ledger entry under `docs/ledger/`.
