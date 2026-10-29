## Context

`IsPatternStrategy` converts the input of `x is pattern` and of `x is Type` to text, builds the test over it
with `PatternConverter`, and decided single evaluation itself: a placeholder `$v<span>` stood for an input
that was not a path of names, and a regular expression counted the placeholder's mentions to choose between
an arrow `(($v) => test)(input)` and writing the input in place. A path of names was never bound. The IR
writer has owned that decision for every other translation since the template (`JsExpr.Template`,
`JsExprWriter.RenderTemplate`): a part read more than once is bound once, and only a plain name or a literal,
which nobody can see read twice, is written in place. A member read is not one, since a getter may count.

## Goals / Non-Goals

**Goals:** the input of an `is` is read once, whatever the pattern's shape, by the writer's rule; an input the
pattern never names is evaluated; the transpiled pins change only where an input was read twice.

**Non-Goals:** a member read inside the pattern (#1150). The switch expression and the switch statement
already hold their input in `$s`, read once.

## Decisions

**How Flutter (Dart) answers it.** Dart 3 evaluates the value a pattern matches once, before any subpattern
reads it, and C# makes the same promise through its decision graph. eqc meets it for the input here; the
members a pattern reads inside it are #1150.

**The test is written over a placeholder, which becomes the template's hole in its code.** Writing the test
with `{0}` for the input would hand `{0}` to every helper `PatternConverter` calls, and one that built a
template of its own around it would take the hole for its own. The placeholder is a name no C# identifier
can take, and it becomes the hole only where the test's code names it, never inside a string
(`JsExprWriter.CodeOf`).

**A template's holes are code.** The pattern's test holds the constants it compares with, and a string
constant may spell `{0}`. The writer reads holes in the code of a template's text only, which no other
template's output changes: none places a hole inside a string. A template literal's interpolation is code,
and its hole is filled.

**The writer names a bound part against what the template's text declares.** A function the text
defines that declared the writer's `$0` would take a hole's reads inside it for its own. A pattern's
constant writes one (`x is (Level.Low + 1)` lowers the enum's arithmetic with an arrow over `$0`), so the
writer binds the part to a name the text does not declare (`$$0` there), and no template can capture its
own part. A refusal in the strategy, tried first, refused that valid pattern.

**An input the pattern never names is evaluated before the test.** `({0}, test)`, unless the input is a name
or a literal, whose read nobody can observe (and which TypeScript reports when it stands left of a comma).

## Risks / Trade-offs

A path that is a field is now bound when the test reads it twice, though reading a field again could not be
observed: the writer cannot tell a field from a property by the node, and binding costs an arrow where the
old rule cost a wrong answer.
