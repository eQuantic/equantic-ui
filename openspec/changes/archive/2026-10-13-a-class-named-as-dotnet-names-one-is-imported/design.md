# Design

## Context

See proposal.md. The import collector works on the names a module's source mentions. Two rules skipped
a name before the collector asked the one question that settles it, whether the build writes a module
of that name (`IsAppModule`): the list of .NET's names, and "ends in Exception".

## Goals / Non-Goals

**Goals:**

- A type the build writes is imported where a module names it, whatever it is called.

**Non-Goals:**

- Removing the list. A name with no module behind it still needs it: `DateTime` in a snippet compiled
  with no references must not become an import of `./DateTime`.

## Decisions

**The name yields to the module's own symbols.** Both rules now ask whether the module being written
names, by a symbol of its own, a type declared in source under that name (`NamesAppType`). The first
form asked only whether the build writes a module of the name, and for the app's own types that is
asked by the name alone: an app with a class `Math` and a file that calls `System.Math` had that file
import the app's `Math`, which hides JavaScript's own inside the module. Found in review, before any
release. A type of a referenced project was known by symbol already (`ReferencedNames`). Nothing else
changes: the emitted text is the same, and the import filter still keeps only names the text holds.

**The class of names, not the one reported.** The report was `Type`. Six names of the list are held
by a test, each as a library's class a page calls, with the app's own class named `Type`, a library's
exception class, and a `Math` of the solution's beside a file that calls `System.Math`. Each fails on
main's emitter, and the last fails on the first form of the fix too.

## Risks / Trade-offs

- A file that names both the app's `Math` and `System.Math` imports the app's, and the import hides
  JavaScript's own for the whole module. C# tells the two apart by their namespaces, and a module has
  one binding of the name. Not measured, and a file that needs both is rare.
