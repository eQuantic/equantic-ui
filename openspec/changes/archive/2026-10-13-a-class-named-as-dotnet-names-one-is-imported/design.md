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

**The name yields to the module.** Both rules now apply only when the build writes no module of the
name. The question was already there, a few lines below, for every other name. Nothing else changes:
the emitted text is the same, and the import filter still keeps only names the text holds.

**The class of names, not the one reported.** The report was `Type`. Six names of the list are held
by a test, each as a library's class a page calls, with the app's own class named `Type` and a
library's exception class. Each fails on main's emitter.

## Risks / Trade-offs

- An app that declares a class called `Math` or `Console` now imports it, and the import shadows the
  JavaScript global of that name inside the module. That is what the C# says: the app's `Math` hides
  `System.Math` there too.
