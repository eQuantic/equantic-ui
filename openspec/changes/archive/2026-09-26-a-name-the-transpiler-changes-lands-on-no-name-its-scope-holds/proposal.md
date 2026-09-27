# Proposal

Closes #465, a Bug under #164 (The transpiler's fences hold on every path).

## Why

A C# name the transpiler changes on its way into JavaScript could land on a name its scope already
held, and C# sees none of it. A local function is camel-cased as a method is, and C# is
case-sensitive, so a local `d` beside `int D()` was one name in the module, a SyntaxError that kept
the page from loading. A reserved word took a trailing underscore, a name C# can write too, and a
verbatim keyword (`@class`) was declared as its source text. A component constructor bound its
parameters camel-cased while its body read them as written. Measured on main at d4fdd6c7:
`LocalNameConformanceTests` had 32 cases, and 29 of them failed there.

## What Changes

- **One owner for a local function's name.** `LocalFunctionName` names a member's local functions
  once: camel-cased, made a legal JS identifier, and renamed with a `$` where the member already
  declares that name. The declaration, a method group and a direct call all ask it.
- **A reserved word takes a `$`**, a spelling no C# identifier holds, for every reserved word and
  literal a module refuses, on every kind of declaration that writes a name.
- **Every declaration is bound under the name its readers use**: a component's constructor
  parameters, a record's method and constructor parameters, a derived record's call to its base, a
  server action's stub and the call it forwards, and pattern, deconstruction, loop and catch
  variables.
- **From the seventh review round**: the spelling of a parameter the body never reads is reserved,
  whether `Build` reads its parameter is asked of the symbol rather than the spelling, and with no
  semantic model a local or a parameter the member declares is a binding.

For a developer using the SDK: C# names that were legal and broke the module, or read the wrong
value, now answer as .NET does. Nothing is written differently. The part reached is eqc. No
committed twin carries a renamed reserved word, and none changed.
