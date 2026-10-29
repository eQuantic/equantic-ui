## Context

A property pattern `{ Count: > 0 }` reads a member of the value it tests, as `x.Count > 0` reads it. eqc
translates a member access through a chain of strategies keyed on the member access's syntax and the model's
symbols: `TupleMemberAccessStrategy` (`t.Item1` is `t[0]`), `DictionaryStrategy` (`map.Keys`), the date and
time strategies with their fences, `QueueStackStrategy` (a sorted set's ends), and the fallback,
`MemberAccessStrategy`, with `CountSpelling`, a field's slot, an extension property's home, the host-only
fence and the read fence (EQ2004). `PatternConverter` kept a copy of a part of that chain for patterns,
and every strategy the copy did not hold was a disagreement.

## Goals / Non-Goals

**Goals:** one translation of a member read, for a member access and a pattern alike, with the receiver's
type the model gives at every depth.

**Non-Goals:** a pattern's subject evaluated more than once, which is another issue's.

## Decisions

- **The pattern's read IS a member access.** It is written as `$pattern.Member`, a synthetic member access on
  a placeholder, and handed to the converter. The placeholder's translation is the value already translated
  (the converter's node cache holds it, so the receiver is spliced as it was written), its type is the type
  of the value read from, and the access stands, through `SemanticHelper.MapSynthetic`, for the in-tree node
  that reads the member: the name before the colon for the first member of a path, and the path's own member
  access for each one after, so a refused chain is told once, at its root. It is the mechanism `?.` already
  uses (`ConditionalAccessStrategy` rebuilds its tail on a placeholder for the same reason).
- **Not one shared table.** The alternative was to move each strategy's read into a function of a symbol and
  a translated receiver, and call it from both places. That is a rewrite of every strategy that reads a
  member, and a table the next strategy has to remember to join; dispatching the read joins every strategy,
  the next one included.
- **The type from the bound tree.** `IRecursivePatternOperation.NarrowedType` is the type the pattern reads
  from, at any depth, and replaces the `accessType` handed down from the subject. A nullable value's members
  are its value's, as C# reads them in a pattern after the null test.
