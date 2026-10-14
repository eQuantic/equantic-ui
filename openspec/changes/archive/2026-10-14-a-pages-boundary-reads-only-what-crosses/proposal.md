# Proposal

Closes #816 and #823, two Bugs under #565 (the transpiler's fences hold on every path, continued).

## Why

What a page's module holds is exactly what crosses to the browser, so the build has to check exactly
that code: refuse what crosses wrongly, and nothing that never crosses. Two fences did otherwise.

The client boundary (EQ2102–EQ2107, EQ2112) was a validator of its own that walked every invocation of
the FILE, for stateful components only. Measured on main at dfce11580, through the build loop eqc runs:

- A stateful page whose `[ServerAction]` reads a file is refused (EQ2112), and so is a `[ServerOnly]`
  member and a `[ServerOnly]` class declared in the page's file, though none of them crosses.
- A stateless page is never read by it: the same call is EQ2004, an untranslated call, with a message
  that tells the reader to add a strategy.
- A construction (`new FileInfo(p)`, `new HttpClient()`), a static read (`Path.DirectorySeparatorChar`)
  and a method group (`File.Exists`) are no invocations, so on either kind of page they build, and the
  module names a class the browser does not have.
- The prefix `System.IO` is also the start of `System.IObservable`, whose `Subscribe` was refused as
  file-system access.
- A class, a static class and a record write their `[ServerOnly]` methods into their twins, so the server
  code in one is refused there (EQ2004), where a component leaves the same method out.

The kept-out fence (EQ2010) said nothing of a type the compilation in hand declares, on the reasoning
that the parser writes no module for it. That holds for the module and not for the reference: a page
that calls the app's own top-level `[ServerOnly] static class Vault` builds with no diagnostic and
writes `Vault.key()`, and the browser throws `Vault is not defined`. The same `Vault` in a referenced
project is refused. A nested type of a kept-out owner is refused since #584.

## What Changes

- The client boundary is asked where the conversion writes a symbol, from the sites that already ask the
  host-only fence: a call, a read, a construction, a method group, `using static`, an operator, an
  extension. So the code checked is the code converted: every member of a component its module holds,
  stateful or stateless, and every class, record and static class a page reaches. The validator is gone.
- The table of server namespaces is matched by namespace, a whole one or a type of it, never by a prefix
  of a display string, and an exception of those namespaces stays a value a page may throw and catch.
- One diagnostic per site: a member of an API only a server has is never also told as untranslated
  (EQ2004), and a type of source is refused once, where its name is written.
- A `[ServerOnly]` method, operator or conversion reaches no twin, whatever type declares it, and nothing
  that reads a module for its imports or its refusals reads it, nor a component's Server Action body.
- A type of source the build writes no module for, because it or a class it derives from is
  `[ServerOnly]` or an attribute, or because it is declared inside such a type, is refused with EQ2010
  wherever client code writes its name, a top-level one as much as a nested one, in the app as in a
  project it references. A component crosses whatever it is marked. A position the module writes as
  nothing is free.

For a developer using the SDK: a page may hold a Server Action or a `[ServerOnly]` member that reads a
file, a database or the network, as CLAUDE.md's own Server Action example does, stateful or not. A
class, a static class or a record may hold a `[ServerOnly]` method that does. Client code that reaches
the server platform is refused on a stateless page too, where it was EQ2004, and so is a construction, a
read or a method group that built before and threw in the browser. A page that names the app's own
`[ServerOnly]` type or an attribute class, and a type that crosses and writes the zero of a
`[ServerOnly]` struct, fail the build with EQ2010 where they built and threw. The boundary codes keep
their texts, and EQ2010 says of a type of source what it said of a nested one.

Parts reached: eqc only. The developer surface does not move. The public surface of the compiler, a
tool assembly, does: `SemanticValidator` goes, and `RuntimeProvidedTypeScanner.Collect` takes whether the
declaration it reads is a component's. No app calls either. Migration line: a page that named a
`[ServerOnly]` type of its own app, or an attribute class, calls it from server code instead, a Server
Action or a `[ServerOnly]` member.

## Capabilities

### New Capabilities

- `client-boundary`: which code the build reads as client code, what it refuses there, and what it
  never refuses because it never crosses.

### Modified Capabilities

None.
