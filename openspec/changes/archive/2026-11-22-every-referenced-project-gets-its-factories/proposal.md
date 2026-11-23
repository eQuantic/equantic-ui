# Proposal

Closes #1803, a Bug under #1587 ("The transpiler's fences hold on every path, part five"), left by #818.

## Why

Only the first project of an app's references got its components' factories, and nothing said so: a page
that composed a component of any other project failed with CS0103 at the call. A real site's app
referenced six projects, the first of them (`Application`) with no component, so its shared library's
`Logo` and `FramedImage` got no factory and the site kept hand-written ones.

The SDK handed the generator the projects' assembly names joined with `;` in a compiler-visible property,
which the compiler reads from the analyzer config MSBuild writes. An analyzer config ends a value at `;`
or `#` and reads the rest as a comment, so the generator was handed the first name alone. Measured: with
the names joined by `,` instead, an assembly named `Brand#Lib` still loses its factories, so another
separator only narrows the failure. The unit tests handed the generator the list through a fake options
provider, which cuts nothing, and `samples/SharedLibrary`'s project with components comes first in its
list, so nothing failed.

## What Changes

- The SDK writes the names one a line in `equantic.factory-projects.txt`, under the project's
  intermediate folder, and hands the file to the compiler with the additional files, the way .NET hands
  a generator data it reads. The generator finds it by its name and reads every line. No character a
  name can hold cuts the list, and a design-time build hands the compiler the same file, so an editor's
  compilation has the factories too.
- The compiler-visible property `_EqReferencedProjects` goes. It was internal and never part of the
  developer surface.
- A developer writes nothing: every project the app references gets its components' factories, wherever
  it comes in the list.

The web SDK's targets and props and the factory surface's generator are the only parts reached. The
public surface and the developer surface do not move.

## Capabilities

### Modified Capabilities

- `component-factories`: the factories of a referenced project do not depend on where it comes in the
  app's references, nor on the characters its assembly's name holds.

## Principles

The structural fix rather than the patch (docs/PRINCIPLES.md): the list reaches the generator through
the mechanism .NET has for a generator's data, additional files, rather than a value whose format cuts it
at a character, and the test builds through MSBuild and the compiler rather than through a fake that
cuts nothing. Rule 10 still holds: the set is the one the build already resolved.
