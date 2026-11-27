# Design

## Who knows what `./Order` means

The writer that wrote `Order`. A construction knows the type it builds, an annotation the type it annotates, a
class header its base. A walk of the declaration's syntax names more than the module writes, and a name alone
cannot tell the app's `Order` from a referenced project's. #830's own measurement showed a rule by name (the
import means the type its own project declares) breaks the collision tests, where a page really writes both.

So each writer records the symbol it wrote, once per write, under the twin's name (`WrittenTwins`, on the
conversion's context, cleared with it per module). `RegisterIntroduced`, which every writer of an introduced
name calls, records there, and so do the writers that build the name as text: `new`, the emitter's annotations
(`DeclaredType`, a field's, a property's, a parameter's, a record member's, a local that starts null), a class's
base in all three emitters, a static call's home, a type named bare, a hydration spec, a union alias's cases.

## When the record decides

Only when it is complete for the name. The module's text is counted: every use of the name that is not a member
(after a dot) and not a string holding only a name (`'Order'`). Where the uses equal the writes on record, the
import means the recorded types. Otherwise some writer wrote the name and said nothing, and the import means every
type of the name the declaration's symbols name, which is what it meant before. Counting errs one way only: a use
counted that writes nothing (a comment, a string with more than a name) sends the module back to the walk; a use
is never missed, so a write nobody recorded is never decided away.

A write recorded for text that was then dropped is counted too, and sends the module back to the walk. The case
the count cannot see is a dropped write of one type exactly matching an unrecorded write of another under the same
name. It needs a writer this change does not record, and the baseline below makes each of those visible.

## Holding the rest

`CompilationResult.ImportsOnTheWalk` lists the imports still decided by the walk. `ImportsOnTheWalkTests` reads it
over the shared library, the emission shapes and the fixtures, and compares it with
`imports-on-the-walk.baseline.txt`, which may only shrink: a new writer records its writes, or the test names it.

## Flutter

Dart has no flat module namespace: an import names its library by path, so a name never maps back to a
declaration. The closest idiom here is that the import travels with the symbol that made it.
