# Design

## How Flutter solves it

Flutter's hot reload hands each save to the Dart frontend server, which keeps the program compiled
between saves and invalidates the libraries whose source changed. When a library's outline (its
declarations, the shape other libraries compile against) is the same after the edit, the libraries
that import it keep their compiled code, and only the edited one is compiled again. When the outline
changed, its importers are compiled again too. `docs/FLUTTER-PARITY.md` has no row for hot reload.

eqc has the same two halves. A file's declarations are what other files bind to. Its bodies are read
by another file's compile in a few places only, and those places are found by recording them rather
than by listing them.

## The rule

A served build that kept the compilation (#1278) knows which files a save changed. For each of them it
compares the text outside every body before and after the save:

- A body is the block or the expression body of a method, a constructor, a destructor, an operator, a
  conversion and an accessor. Everything else is outside: attributes, modifiers, signatures, base
  lists, field and property initializers, enum values, usings, top-level statements, and the trivia
  between them (a documentation comment included).
- When the text outside the bodies differs in any changed file, the build compiles every file, as a
  cold build does.
- When it is the same in every changed file, no other file binds to anything new: C# reads another
  file's declarations, never its bodies, to bind. The build compiles again the changed files, every
  file whose last compile read one of them, and every file whose last compile failed. Every other file
  keeps what it produced.

## What a compile reads

A file's module depends on its own text, on the declarations it binds to (the same, by the rule), and
on the syntax of other files the compiler read while it compiled it: a default interface member's
body, a struct method's body that `ValueCopies` reads to learn whether the method writes its value
(recursively, into the methods it calls on that value), a base record's declarations, a constructor's
declaration. Those reads are recorded as they happen:

- `SourceReads` is the record of one file's compile. AppBuild opens one around each file and keeps
  the paths it collected beside the file's results.
- Every path from a symbol to syntax goes through `SymbolSources`: the declarations' references and
  the locations whose tree or position is read note their file. Whether a symbol is declared in
  source, and how many declarations it has, are declarations and note nothing.
- A cache that keeps an answer read from another file's syntax (`ValueCopies.Mutating`,
  `ExplicitImplementations.Cache`) keeps the paths it read beside the answer, and notes them into
  whichever compile takes the answer. Without that, the first file to ask would record the read and
  every later one would not.
- A guard reads the compiler's sources and fails on a raw `DeclaringSyntaxReferences` or a read of a
  symbol's `Locations` outside `SymbolSources`, and on a cache keyed by a symbol or a compilation that
  is not listed with the reason it keeps no answer read from another file.

## What a skipped file still gives the build

A file the build does not compile again still contributes what its compile contributed: its results
to the twin claims, the entry points and the referenced types to compile, in the order a cold build
gives them, and its resource uses and discovered resource classes to the string catalogs. AppBuild
keeps those per file and replays them into the compiler for a file it does not compile.

## What it costs

Recording a read is a set insertion. The record and the replayed results live only in a served eqc,
for the files of the app, and hold strings: no symbol, tree or compilation outlives its build.
