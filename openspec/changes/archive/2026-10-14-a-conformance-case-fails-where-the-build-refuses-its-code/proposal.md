# Proposal

Closes #907, a Bug under #849. It settles #761, a Bug under #565, from the runner's side: the three
queries it measured are refused by the build (EQ2008), and the suite now pins that refusal.

## Why

The conformance harness transpiled each case with `CSharpToJsConverter` and ran what it wrote
without reading `converter.Diagnostics`. Where the build refuses a construct with an error (a fence
such as EQ2008, a member with no translation such as EQ2004), the converter still writes text, its
fallback, and the runner executed it as if it had compiled. A case could then report a wrong answer
no build lets reach a browser, or pass on code no app can ship. #761 is the measured example: a query
with `let`, a second `from` or `into` answered something else in the runner, where the build refuses
all three with EQ2008.

## What Changes

- Every entry of the harness that transpiles a case reads what the build refuses in it, and a case
  the build refuses fails before either side runs, naming each refusal by its id and text. The
  refusals are read where the build reads them and by its rule: the converter's errors over the text
  the case would run (an error refuses, a warning does not, as `ComponentCompiler.CollectDiagnostics`
  draws the line), and the build's own verdict on the declarations of the case's prelude, asked of
  `ComponentCompiler`, so every check a build runs beside the converter (EQ1007, EQ2009 and the rest)
  is read and none is encoded a second time.
- A case written for a fence says so: `ConformanceRunner.AssertStatementsRefusedByTheBuild(statements,
  id)` and `AssertRefusedByTheBuild(expression, id)` require C# that compiles and a refusal by that id
  and no other, and run nothing. `Transpiler.TranslateStatements` and its siblings answer the text
  with its refusals for a case that pins the text of a refused construct.
- The harness paths that compile a source as an app's build does (`ModuleGraph`, and four places that
  call `ComponentCompiler` themselves) read every module's errors before they keep the modules that
  wrote text, so a module the build stops at before its emitter runs no longer takes its refusal
  with it, and each failure names the id.
- The suite with the rule: the three queries of #761 pinned as EQ2008, and the cases that ran a
  refused construct sorted, each either rewritten in C# the build accepts, pinned as the refusal it
  documents, or pinned until the refusal it reveals is fixed under an issue of its own.

For a developer who uses the SDK: nothing changes. This reaches the conformance test project only:
not eqc, the runtime, a realizer, a shell, an SDK or a template, and neither the public surface nor
the developer surface moves.

For whoever writes a conformance case: a case whose C# the build refuses fails, naming why. A case
meant to pin a refusal declares it with the id.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `working-agreement`: a conformance case runs only C# the build accepts, and a case written for a
  fence declares the refusal it pins.
