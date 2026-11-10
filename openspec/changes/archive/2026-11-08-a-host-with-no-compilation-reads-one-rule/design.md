## Context

An app built with the SDK always hands eqc the project's compilation. The hosts that do not are the playground's
`CompileSource` and tools over the compiler, which read each file with a minimal model and the scan by names.

## How Flutter answers it

`dart analyze` and the compiler read one element model, so the question does not arise. Here the host with no
compilation is the exception, and the rule is at least one rule.

## Decisions

- **#754 refuses rather than writes the class.** The issue offers both. Writing the class needs the host to know a
  base's constructors and every module to import it, in a host with no scan; refusing tells the author at compile
  time what failed in the browser, in the fence every member already passes, and the playground shows it.
- **The name list is the union.** The parser's lacked `ComponentState` and `UiComponent`, which the scan had, and
  which the symbol path reaches anyway. `CreateState` left it: a class is not a component by that name alone.
- **The fallback for `[RuntimeProvided]` is gated on the host**, so a build with a compilation stays answered by the
  semantic scan alone. What the module does not name is taken out by the filter already there.

## Risks / Trade-offs

- A snippet in the playground that used such a member now fails to build. It never ran.
