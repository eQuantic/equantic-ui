# Design

## A runtime call, not an inline expression

Both lowerings were inline JavaScript that had to name a temporary (`_idx`) or approximate a grammar
in one expression. A runtime function binds its temporaries where no C# name can see them, which is
what the issue asked of `Remove` (#397 is the class of bug an inline temporary invites), and it is
where the numeric readers already live. The call evaluates its arguments left to right, which is
C#'s order.

## Equality for Remove

`List<T>.Remove` compares with `EqualityComparer<T>.Default`. On this side a twin with its own
`equals` (a record, a struct, a decimal, a date, a class that overrides `Equals`) is asked, a number
compares as a double's `Equals` (NaN equals NaN), and everything else by identity, which is what a
class that does not override `Equals` compares by. An array is compared by identity too, as C#
compares an array.

## Bool text

.NET's `Boolean.TryParse` compares the text with "True" and "False", then again after trimming white
space and NUL characters from both ends. The comparison is ordinal and ignores case, and measured on
.NET 10, it folds nothing non-ASCII onto those two words ("Falſe", with a long s, is refused), where
JavaScript's `toUpperCase` makes a long s an S. So the twin folds ASCII letters only.

`bool.Parse` and `bool.TryParse` have the shape the numeric readers have (a text, and a TryParse
whose `out` takes the value or the type's zero), so they join that strategy rather than keep a
second copy of its `out` handling.

## Not here

`Convert.ToBoolean(object)` still reads a null and any text as true; it belongs with the other
`Convert` overloads of an object (#401), where the measurement is recorded.
