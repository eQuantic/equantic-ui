# Proposal

Closes #397, #467, #556 and #657, four Bugs under #565 (The transpiler's fences hold on every path, continued).

## Why

The emitted code declared names of its own as a C# local would be named, and read globals no C# name was kept away from. A C# local of the same name, read inside the code a lowering wraps, read the lowering's binding instead, and one named like a global hid it from the lowering beside it (#397). Measured with the conformance harness on main fcae8f29: a captured `_sum` answered 4 where .NET answers 23, a captured `key` threw before the reduce had set its own, a captured `a` sorted by the comparator's element, a sequence named `x` was the element it was compared with, `var crypto` beside `Guid.NewGuid()` threw, `int undefined` filled the parameter a named argument skipped, and `int Math` beside `Math.Abs` threw.

A name written with C#'s verbatim escape went out with it, or as a word a module reserves: a label `package`, a member `@class` read and set in a `with`, and a method's type parameter `@class` (#467).

A member reached bare through `using static` failed the build with EQ2004 wherever only a member access's strategy translated it: `Now`, `UtcNow`, `Today`, `NewGuid()` and `Empty`, while `DateTime.Now` and `Guid.NewGuid()` translated (#556).

A value C# evaluates once ran once per element: `Intersect` and `Except` wrote their second sequence inside the filter's arrow, `Average` named its source twice, and a selector that is not a lambda was called for every element by `Sum`, `Average`, `GroupBy`, `OrderBy` and the lookup's indexer (#657).

## What Changes

- **Every binding the emitted code declares starts with a `$`**, which no C# identifier holds: the LINQ lowerings, the nullable lifts, the task delay, the string conversions and a struct's zero. A guard reads the compiler's source and fails on a binding without one; a setter's `value` is the one exception, since the developer's setter reads it by that name.
- **A C# local or parameter named like a global the emitted code reads takes a `$`**, as a reserved word does (`crypto` is `crypto$`). The globals are one list, `StringExtensions.EmittedGlobals`, which the local functions' rename reads too, and a guard derives them from the compiler's source and fails on one missing.
- **The template writer binds a part whose hole sits inside a function the template defines**, as it binds a part used twice, so the part is evaluated once, in C#'s order. A lambda written in place stays where it is, since making it again changes nothing and in place it keeps the parameter types a call around it gives.
- **`Sum`, `Average`, `OrderBy`, `GroupBy`, `Distinct` and the lookup's indexer move to the IR**, so the writer does their evaluation too; the text strategies' baseline shrinks by six.
- **A verbatim name is written without its escape**: `TwinName.Of` drops the `@`, as a symbol's name already does, so a member and a `with` key are `class`; a label and a type parameter go through `ToJsIdentifier` (`package$`, `class$`), declaration and references alike.
- **A member reached bare through `using static` is converted as its qualified spelling**, a copy that stands for the bare node, so every strategy that translates the qualified form translates it. One the qualified spelling does not translate either still fails the build with EQ2004, reported where the bare spelling is written.

What does not move: the developer surface. The public API changes where the six strategies change their interface (`Convert` goes, `ConvertIr` comes), and a method group over a platform member reached bare, which never translated, still fails the build. The runtime's twins are regenerated: their bindings take a `$`.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `transpiler-names`: a binding the emitted code declares starts with a `$`, a local named like a global the output reads is renamed, a verbatim name is written without its escape, and a member reached through `using static` translates as its qualified spelling.
- `transpiler-sequences`: a LINQ argument is evaluated once, in the order C# evaluates it.
