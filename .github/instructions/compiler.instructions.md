---
applyTo: "src/eQuantic.UI.Compiler/**,src/eQuantic.Build/**,src/eQuantic.UI.Generators/**,src/eQuantic.UI.Codegen/**,tests/eQuantic.UI.Compiler.Tests/**,tests/eQuantic.UI.Conformance.Tests/**"
---
# The compiler (eqc)

The transpiler is the bar the rest of the SDK is measured against: a strategy per C# construct, an IR
(JsExpr, JsStatement, JsClassMember, JsClass, JsModule) with one writer per level, and coverage suites
whose baselines may only shrink. Flag, as a defect:

- A new strategy that returns text instead of IR. tests/eQuantic.UI.Compiler.Tests/Coverage/ir-migration.baseline.txt lists the text ones and may only shrink.
- A strategy that writes parentheses, evaluates a part twice or lays out statements itself: the writers own precedence, single evaluation (JsTemplate) and layout.
- An implicit conversion (char promotion, int to long, a float result, decimal, a value entering a string) settled in a syntax strategy: ValueFlow owns those from the bound tree (IOperation), never both.
- A decision on a symbol's name where the semantic model can be asked. A name heuristic is legal only where the model cannot be asked; an in-tree call the model cannot bind is EQ2006, never a guessed translation.
- A translation that differs from what .NET does. The answer is .NET's, measured, not assumed; integer widths wrap by result type, a float is a single wherever it is produced.
- A translation change without a conformance case that executes both sides, or a layout change that moves a pin by more than whitespace (EQ_UPDATE_TRANSPILED regenerates the pins).
- A new static XxxHelper class: predicates on symbols are extension methods, shared behaviour a base class.
- A diagnostic added without its entry in docs/DIAGNOSTICS.md and the diagnostics baseline.
- A second top-level type in a .cs file without a hand-written entry, with its reason, in one-type-per-file.baseline.txt.
- C# rewritten to fit the transpiler (in a sample, a component, a test) instead of the gap being fixed or filed.
