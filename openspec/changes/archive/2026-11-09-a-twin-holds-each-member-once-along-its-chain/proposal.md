## Why

Closes #1172 and #1173, sub-issues of Feature #849, and #1175, a sub-issue of Feature #1174 (the transpiler's fences hold on every path, parts three and four).

EQ1007 refuses every pair of members of one type that lowers to one key of its twin (#1146). Three
shapes of the same rule were left over, each measured with `new ComponentCompiler().CompileSource(…)`:

- A partial property, indexer or event is one member in C#, a defining half with no body and an
  implementing half with one, and each emitter wrote the defining half as a member of its own: a class
  held `level` on each instance over the accessors, so `g.Level = 3` read back 3 where C# reads 6, a
  record's constructor ran the setter and its equality counted the state twice, a partial indexer was
  refused as an overload of itself, and a partial event was written as a field (#1172).
- Along a class chain only two methods were compared, so `class Base { public int Size => 3; }` beneath
  `class Derived : Base { public int size() => 4; }` passed the build, and `new Derived().Size` answered
  a function (#1173).
- The static backing-field idiom, `private static int count;` beside `public static int Count`, was
  refused, where an instance field in the same place moves to a slot of its own (`count$`, #396), and
  the developer renames nothing (#1175).

## What Changes

- A partial property, indexer or event is written from its implementing half, by every emitter (a
  class, a static class, a record, a struct, a component), and judged as one member by EQ1007
  (`PartialMemberExtensions.TwinMembers`, which each pass that writes or judges a twin's members reads).
- A static field moves to `name$` where a static member of its type or of its chain holds its twin
  name, a constant's included, in every type eqc writes a twin of: a static is the class's, read by
  nothing that hydrates, compares, prints or copies an instance. Every declaration, every initializer the
  type initializer orders, and every access follows the slot (`FieldSlotExtensions.TwinSlot`). A static
  auto-property is the member callers read by its name, as an instance one is, and stays refused beside
  a static method of its name.
- A property, an event, a field or a method on the key a member of a base the source declares holds is
  refused with EQ1007, naming both members and the base's file and line, statics included, where one of
  the two is not a method (two methods were already judged). An override, a method that hides a member
  and holds a name of its own, and a field that moves stand (`OverloadedMethods.CheckInheritedKeys`).
- For a developer: the backing-field idiom builds on a static, a partial member answers as C# does, and
  a derived member that would take its base's key fails the build instead of answering for it.

## Impact

- Reaches `eQuantic.UI.Compiler` (`FieldSlotExtensions`, `PartialMemberExtensions` (new),
  `OverloadedMethods`, `MembersOnOneName`, `TypeScriptEmitter`, `RecordTypeEmitter`, `TypeInitializer`,
  `ComponentParser`, `IdentifierStrategy`). No runtime, realizer, shell or SDK change.
- No new diagnostic: EQ1007 covers more pairs along the chain and fewer static ones, and
  `docs/DIAGNOSTICS.md` and the wiki's Diagnostics page say which.
- `PublicAPI.Unshipped.txt` of `eQuantic.UI.Compiler`, a tool surface, gains `StateField.Slot`. The
  developer surface does not move.
- A break in preview: a derived member on its base's key, which built and answered wrong, now fails
  with EQ1007. Migration: give one of the two its own name.
