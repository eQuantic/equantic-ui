## Why

Closes #1138, a bug under #849 (the transpiler's fences hold on every path, part three). `Temporaries.Reset`
promises that a module numbers its own temporaries from `$n0`, and the component path keeps it: the emitter
resets the converter (`ConversionContext.Reset`) before it converts a component. The class path does not.
`EmitClassModule`, behind `EmitPlainClassModule` and `EmitStaticHelperModule`, cleared the names it used and the
diagnostics, and nothing else, so in one build a plain class's or a static helper's module numbered its
temporaries after the module emitted before it, and the node cache kept every earlier module's syntax trees
alive.

The committed twins show it: `SheetController.ts` began at `$n88`, `CodeSnippetCompletionProvider.ts` at `$n54`,
`CodeSignatureHelpSession.ts` at `$n46`. An edit to one class (`CodeHoverSession.cs` in #1008) renumbered every
plain-class twin written after it, so two open pull requests conflicted in twins neither of them touched.

## What Changes

For a developer using the SDK, nothing to write, and nothing the browser does differently: a module's text no
longer depends on what the build emitted before it, so a change to one class changes that class's module alone.

- **Every module the emitter writes starts from a reset converter.** One method of the emitter,
  `BeginModule`, is where a component's module and a class's start: the converter's reset, then the module's
  annotations, model and class. A class's module handed no model converts with none, where it kept the model of
  the module before it.
- **The record emitter's module does the same.** `RecordTypeEmitter.EmitModule` resets the converter it is
  handed. No build showed the omission, since the compiler hands each record a converter of its own, and a host
  that shares one (the conformance harness) met the same numbering. `Emit`, which writes one class and may
  write several into one module, does not reset.
- **The runtime's twins are regenerated.** Nine twins under `src/eQuantic.UI.Runtime/src/shared/components/`
  change, by the names of their temporaries alone.

The parts it reaches: eqc (`TypeScriptEmitter`, `RecordTypeEmitter`) and the runtime's committed twins. The
public surface does not move, nor does the developer surface. Nothing an app wrote breaks.
