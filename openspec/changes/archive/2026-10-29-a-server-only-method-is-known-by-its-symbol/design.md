# Design

## Context

`[ServerOnly]` on a method is a contract between the author and the build: the method never reaches the
browser. The build decided it in one place, `ComponentParser.StaysOnTheServer`, by reading the attribute
lists of the declaration in hand by name (`AttributeSyntaxExtensions.IsNamed`). That rule is the one the
repository allows only where the model cannot be asked, and every reader of it had a model in reach.

## How Flutter solves it

It does not have the problem: Flutter has no server half of an app that the same source declares, so
there is no boundary for a method to sit on. The .NET side decides this one.

## Decisions

### Ask the symbol, as the class-level check already does

`ComponentParser.IsServerOnly(ClassDeclarationSyntax)` asks the type's symbol and reads the syntax only
where no model answers, and the method rule now does the same. Roslyn answers both spellings the syntax
missed, measured: an alias resolves to `ServerOnlyAttribute`, and both halves of a partial method return
the attributes of the two declarations merged. Without references (a standalone `CompileSource`, the
playground) the attribute binds to an error type that still carries its name, the alias target's for an
alias, so the symbol answers there too.

### One parameter, required

`StaysOnTheServer` takes `SemanticModel? model` with no default, so no reader can keep asking the syntax
without saying so. The rule resolves the node's own model from the one it is handed (the same tree, or
`Compilation.ModelOf` for another), as the emitters' `ModelFor` does, so a scan hands the model of its
root once.

### Bind nothing for a member that says nothing

A method that writes no attribute list and is no partial method can carry no attribute, and is answered
`false` before any model is asked. The scans that ask the rule of every node of a declaration
(`NamedSourceTypes`, `RuntimeProvidedTypeScanner`, the emitter's type collection) and `ServerOnlyContracts`,
which reads every method of the project, bind only the members that could be marked.

## Out of scope

`[ServerAction]` on a component's method is still read by name. A Server Action is the component's alone
and its stub is written from the syntax; an alias for it is a question of its own.
