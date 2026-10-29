# Design

## Context

`OverloadedMethods.Check` runs before every emitter and already refuses, with EQ1007, two methods of
one name (`CheckOne`), a record's or a struct's state beside a member (`CheckState`), a class's held
primary constructor parameter beside a member (`CheckHeldParameters`), and pairs along a chain and among
interface defaults. Each of those passes enumerates a slice of a type's members by hand. Nothing
enumerated the rest.

## Decisions

**One key table, built the way the emitters name.** A new pass, `MembersOnOneName`, puts every member a
twin holds on its key, read through the functions the emitters themselves call: a field's slot through
`FieldSlotExtensions.TwinSlot` (so a plain class's field that moved to `name$` is on its own key, and a
component's, a record's and a static's are not moved), a method through `TwinMethodName.Of` (a hiding
method's `name$1`, a typed `Equals`'s `equals$1`), an operator through `RecordTypeEmitter.OperatorMethodName`
and a conversion through `ConversionNameFor`, an extension block's property as the static the static
helper emitter writes, its indexer as `item`. Reading the emitters' own functions is what keeps a pair
the emitter makes harmless out of the refusal, without a list of exceptions to drift.

**The same code, EQ1007, widened.** The failure is the one EQ1007 names: two members, one name on the
twin, the browser keeping one. The remedy is the same (rename one). A second code would split one rule
in two and make a reader ask which applies. It is a gap and not the target's shape, as the overload
case is, so EQ1xxx stays right.

**The existing passes stay first.** `CheckOne`, `CheckState` and `CheckHeldParameters` run before the new
pass and keep their measured messages; the new pass judges what is left, and `Check` stops at the first
pass that refuses. Explicit interface implementations stay out, as `TakesAName` leaves them out, since
#1139 owns them.

**A name the twin holds itself is a member like any other.** `constructor` (every class), `static
prototype` (every class, JavaScript refuses it), a value's `with`, and the four names the runtime and
JavaScript ask (`equals`, `getHashCode`, `toString`, `toJSON`) are on the table before any member. Only
an override of object's `Equals(object)`, `GetHashCode()` or `ToString()` stands there, and
`TwinOwnMembers` is the one predicate for it: the record emitter writes its own member unless the type
overrides it, and the guard lets the override stand by the same test. That moved two emitter decisions
onto the predicate: a struct's `Equals(object)` override no longer has the twin's `equals` written
beside it, and a record's synthesized text is suppressed by an override of `ToString()`, not by any
member named `ToString`.

**How Flutter answers it.** Dart refuses two members of one name in a class at compile time ("The name
'x' is already defined"), so the question never reaches a runtime. This is the same answer at the one
point where C# and JavaScript disagree on what a name is: the build.

## Risks

A pair the guard refuses that the emitter in fact keeps apart would be a false refusal. Every SDK source
the runtime embeds (Components, Charts, Spreadsheets, the Code and Sheet engines, Forms), every sample
and every template transpile with the guard on and are refused nowhere, so no false refusal is known.
