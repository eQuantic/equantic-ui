# Design

## Context

See proposal.md. The compiler converts each module's code through strategies, and the host-only fence
(`HostOnlySymbolExtensions`) is asked from every branch that writes a symbol's name: a qualified call,
a static read, a bare call through `using static`, a method group, a construction, an operator, and an
extension's home. The kept-out rule (#584) is asked where a TYPE's name is written: an identifier that
binds a type, a qualified name, a construction, a type test, a zero, the twin a conversion introduces.
Each module's import net reports a kept-out twin its text would import. The client boundary was none of
these: `SemanticValidator` walked the syntax tree of the file before the emitter ran.

Next.js draws the same line with `server-only`: a module that imports it fails the build where client
code imports it, and the server module itself is never read as client code. Flutter has no such split,
and its web build compiles `dart:io` against stubs that throw at run time, the failure this SDK moves
to the build. docs/FLUTTER-PARITY.md has no row for it.

## Goals / Non-Goals

**Goals:**

- One question for what client code may name, asked where the conversion writes a symbol, so the code
  checked is the code converted by construction, for every kind of module.
- One rule for what crosses (`ComponentParser.StaysOnTheServer`), asked by the parser, the emitters,
  the import scans and the reader of referenced names.
- One rule for a type the build writes no module for (`PlainClassModule.NeverCrosses`), asked of a
  top-level type as of a nested one, in the app as in a referenced project.
- One diagnostic per site.

**Non-Goals:**

- Translating any member of the server namespaces. They stay refused.
- The annotations a component writes for a type it cannot import (`any`) against the ones a class and a
  record write (the type's name). Each is measured and kept: a position written as nothing is free, a
  position written as a name nothing declares is refused.

## Decisions

### The boundary is asked where the symbol is written, not in a pass of its own

The alternative was to keep the validator and scope its walk to the members the emitter writes, by the
emitter's own predicate. It would still be a second reader of the code beside the converter, and it
would still read invocations only: the construction, the read and the method group that build today are
none. Asked inside the host-only fence, every site that already owes that call asks it, and a site added
later owes the same one call. A member of a server namespace is a host-only symbol in exactly the fence's
sense: the browser has no twin for it.

The two sites that reported and then went on to write the member (a bare call and a bare read through
`using static`) now stop where the fence refuses, as the operator sites already did, so the platform
branch after them no longer tells the same call as untranslated (EQ2004). An extension asks the fence
before its own EQ2004 verdict, which stays first for every home the fence lets through.

### A type of source is the kept-out rule's

The host-only fence refused a referenced project's `[ServerOnly]` type at the member it was reached
through, and the kept-out rule refused a nested one where its name was written. Once the top-level type
is the kept-out rule's too, both would report one site twice. So the type-level half of the fence answers
for metadata types alone, the framework's vocabulary, where a host-only base fences only the members
reached through it (`SingleChildNode.Child`), and every type of source answers by the kept-out rule
where its name is written. The member-level half still comes first for every symbol.

A top-level component crosses whatever it is marked: the parser writes its module, as the attribute's
own doc says. The rule says so in one line rather than asking the parser's syntactic component test.

### What crosses is one predicate

`ComponentParser.StaysOnTheServer(node, inAComponent)`: a method, an operator or a conversion marked
`[ServerOnly]`, and the body of a component's `[ServerAction]`. The emitters leave such code out, and the
import scans (`RuntimeProvidedTypeScanner.Collect`, the type collector of the import net,
`NamedSourceTypes`) do not read it, so a framework `[ServerOnly]` type a Server Action reads is not
refused as a type position of the page, and a kept-out type a `[ServerOnly]` method of a class names is
not refused by that class's net. `RuntimeProvidedTypeScanner.Collect` takes `asComponent` for it, a
signature change on a tool surface.

### Refused exactly where the module names it

Measured position by position. A component's field and parameter are annotated `any`: free. A local is
annotated with the type's name, a class's and a record's member too: refused by the import nets. A
struct's zero has no twin to build it: refused where it is asked, a component's field included, which
now asks at the declaration as a class's and a record's do. A reference type's zero is null and names
nothing: free, where the zero of any kept-out type was refused. A Server Action's answer is hydrated
into the type: refused by the net. Its parameter is annotated with the type's name: refused at the
parameter. A cast is the value, and `nameof` and `typeof` are text: free. A record's net now reports a
kept-out type its body names, as a class's and a component's do; a record's member of a nested kept-out
class was refused by its zero alone until then.

## Risks / Trade-offs

- [A plain class that read a file failed with EQ2004 and fails with EQ2112 now] → The same site, a code
  that names the API. The referenced-project tests that pinned EQ2004 say EQ2112.
- [A page that built over an attribute class or a server-only type now fails] → It threw in the browser.
- [A component's diagnostics are no longer cut short by the boundary] → The validator returned before the
  emitter ran, so a component with a boundary error showed nothing else. Every diagnostic of the module
  is reported now, as for every other module.
