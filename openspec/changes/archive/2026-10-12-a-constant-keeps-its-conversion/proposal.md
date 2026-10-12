# Proposal

Closes #833, #834 and #835, three Bugs under #565 (The transpiler's fences hold on every path,
continued). A review of #827's last commits found all three after that pull request merged, and each
was measured on main at d5097bc2f before this was written.

## Why

Two of the three came in with #827, and the third is older and #827 made it worse for a referenced
project.

A constant handed to a user-defined conversion is written with no import of the type that converts
it (#833). `_board[Limits.First]`, over an indexer that takes a `Slot` with an implicit conversion
from `int`, is written `this._board.item(Slot.fromInt(1))` in a module that does not import `Slot`.
`nameof` does the same. The build succeeds and the browser meets a name nothing defines. #827 taught
the reader of a module's names to leave out a constant that is written as its value, and the
conversion C# applies to that value went with the node.

An interpolation's alignment is written as its source text (#834). `$"{_name,Widths.Name}|"` is
written `$eq.text.format(this._name, null, Widths.Name)`, where a literal alignment is written `12`.
That is every app, with no diagnostic. With a constant of a referenced project the text is
`Columns.Name` in a module that does not import `Columns`.

A list of projects that was handed and holds no project is read as no list (#835). #827 made a
handed list that does not hold stop the build, and left the empty file out: the build goes on with
every referenced project as an assembly, which is the page written with nothing to import.

## What Changes

- A value written for a constant, or for `nameof`, is converted where C# converts it, and the
  module imports the type that declares the conversion, as it does for any other value.
- An interpolation hole's alignment is written as the number it is, whatever the source spells it
  with: a literal, a constant by its name, a negated one, a sum. `StringBuilder.Append` of an
  interpolated string writes the same number.
- A list of projects that was handed and holds no project stops the build with EQ0001, as the list
  that is not there does.

For a developer: nothing to write. A page that hands a constant where a conversion applies runs, an
aligned hole pads as it does in .NET, and a build that lost its list of projects says so.

It reaches eqc alone. The public surface and the developer surface do not move, and nothing breaks.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `referenced-projects`: a value written for a constant brings the type that converts it, and a
  handed list that holds no project stops the build.
- `transpiler-bcl`: an interpolation hole's alignment is written as its value.
