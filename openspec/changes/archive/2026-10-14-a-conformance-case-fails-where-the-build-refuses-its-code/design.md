# Design

## Context

See proposal.md. A real build turns the converter's diagnostics into its verdict in one place,
`ComponentCompiler.CollectDiagnostics`: an `Error` becomes a build error and fails the module, and
anything else is a warning the build prints and builds past. Beside the converter it runs checks of
its own over a type before any emitter: two methods of one name (EQ1007), one name declared twice in
a file (EQ2009), a union's body (EQ2008), and for a component its runtime members (EQ2011), an
abstract component's server action (EQ2012) and, for a stateful one, the client boundary's semantic
validator. `AppBuild` prints the errors of every module that did not succeed, an error with no id as
`EQ0000`.

The statement harness drives the converter itself: it writes a program of its own (the case's body
as the method `__Eval` of a plain class, after the case's prelude) and asks the converter for the
text of the body, and for the prelude's records through `RecordTypeEmitter`. It read none of the
diagnostics. The module harness (`ModuleGraph`) compiles a source through `ComponentCompiler` as an
app's build does, and read each module's errors only after it kept the modules that wrote text.

Flutter has no harness of this shape to ask, and what it shows is the general rule: `flutter test`
compiles a test before it runs it, and a test the compiler refuses fails to load rather than run.

## Goals / Non-Goals

**Goals:**

- No case runs text the build refuses, and a case the build refuses names every reason, by its id
  and its text.
- The harness refuses what a build refuses and nothing else: an error and not a warning, and every
  check a build runs beside the converter where the build runs it.
- A case written for a fence pins that fence, and says so the day it lifts.

**Non-Goals:**

- Fixing the refusals the suite found. Each that a build should not make is filed and pinned as a
  refusal until it is fixed.
- The tests that compile without running: they assert on emitted text or on diagnostics already.

## Decisions

**The refusals travel with the text.** Each entry of `Transpiler` answers a `Translation`: the text
the converter wrote and every refusal, and the runner reaches the text through `Accepted()` only,
which fails the case with `RefusedByTheBuildException` when there is a refusal. The old entries
(`TranspileStatements`, `TranspileExpression`, `EmitDeclaredRecordTypes`) keep their shape and call
it, so every comparison of the suite reads the rule without a change to its case.

**The build's own line between an error and a warning.** `Refusal.Of` keeps a converter diagnostic
whose severity is `Error`, the line `CollectDiagnostics` draws, and a module's errors when it did not
succeed, as `AppBuild` prints them. A number read with no culture (EQ2110, a warning) still runs.

**What a build runs beside the converter is asked of the build.** The prelude's declarations are
compiled as an app's file is, by `ComponentCompiler.CompileSource` over the prelude with a project
compilation of it, and every module's errors are the prelude's refusals. Nothing beside the converter
is encoded in the harness, so a check a build gains is read here the day it lands. The verdict is
asked once per prelude (the cases of a class share a few), by one compiler under a lock: a compiler
reads its standard references into native memory when it is made, which one per prelude would pay
hundreds of times (#481, measured at about 34 MB a compiler), and one instance compiling file after
file is how a build uses it. What one prelude leaves in that compiler decides nothing for the next,
read in `ComponentCompiler` and what it holds: the project compilation is replaced, and so is the
scan of it eqc hands the compiler before it compiles a file (`ComponentDependencyResolver.From`), the
emitter clears what a module reports and names, and what survives is keyed by identity, never read
for an error, or keyed by a name only a type .NET loads answers. The scan is part of the verdict: a
module's import net reads it, and refuses a field typed as a nested type of a server-only owner
(EQ2010) where no fence of the converter looks, which a verdict asked with no scan accepted (found by
Copilot's first review of #914). The harness asks two pairs of preludes that declare one type name
differently, one told apart by the emitter's own check and one by the scan, in both orders, and
each answers its own.

**Nothing beside the converter is asked of the body.** A build would compile the body as the method
of a plain class, the program the harness writes, and runs nothing else over such a method: the
semantic validator returns at once for anything but a stateful component, so reading it would refuse
what a build accepts. No case of the suite calls an API it forbids.

**A refusal a case pins is declared by its id.** `AssertStatementsRefusedByTheBuild` and
`AssertRefusedByTheBuild` take the id. They require C# that compiles (the C# compiler's errors over
the model the converter read, asked only there, since binding a whole compilation for every case is
time the suite would pay for nothing), at least one refusal of the case's OWN C#, the converter's
over the text the case would run, and no refusal of another id, its prelude's included, and run
nothing on either side. A refusal of the prelude alone never stands in for the case's own: a case
whose prelude met the fence would go on passing after the fence lifted for the case itself, so a
`Translation` keeps its own refusals apart from its prelude's. `Transpiler.TranslateStatements` and its siblings answer the text with its
refusals for a case that pins the text of a refused construct, the routing of #394's gap.

**The module harness asks every module first.** `ModuleGraph` and the four tests that call
`ComponentCompiler` themselves read `Refusal.Of` over every result before they keep the ones with
text: a module the build stops at before its emitter runs writes none.

## Alternatives

- **The whole case through `ComponentCompiler`.** The build's verdict over the body too, by
  construction. Rejected: the verdict would be of the plain-class module the build writes around
  `__Eval`, not of the text the runner runs, and every case would compile twice.
- **`InternalsVisibleTo` to call `OverloadedMethods` from the harness.** Rejected: it reads one check
  of the build's several, and the test projects reach the compiler through its public surface.

## Risks / Trade-offs

- The prelude's verdict comes from a compilation of the prelude alone, with the harness's references
  and options. A check that depends on code outside the prelude would read differently there; none
  does today, every one being over the type's own declaration and its bases.
