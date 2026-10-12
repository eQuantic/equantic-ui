# Proposal

Closes #836, a Bug under #565 (The transpiler's fences hold on every path, continued). A review of
#827's last commits found it after that pull request merged, and it was measured on main at d5097bc2f.

## Why

Two values the SDK hands the compiler are cut on the way.

The assembly name goes through a shell. `Sdk.targets` writes `--assembly-name "$(AssemblyName)"`
into an `Exec` command, and the shell reads it before eqc does. Measured on macOS: `Cash$Flow`
arrives as `Cash`, and a name with a backtick fails the command. The app is then compiled under a
name that is not its assembly's, and a library's `InternalsVisibleTo` for the real name opens nothing
(EQ2006). The paths in the same command go through the same shell, so a directory with a `$` in its
name meets the same end.

The list of projects splits at a bar. It is one line a project, `assembly|project file`, and the
reader cuts at the first bar, which a path on macOS or Linux may hold: read alone, a list under
`/work/a|b` is EQ0001 naming `/work/a`. Measured after the issue was filed, the C# compiler itself does
not build a project with a reference under such a folder (CS0006, the reference's path handed to it
without its bar), so this half is the compiler's own reading made whole, and not a build it turns
green.

## What Changes

- The SDK writes what it hands eqc to a response file under `obj`, one argument a line, and the
  command names two things: the compiler, and that file by its name, in the folder eqc runs in. No
  value of the project is written into a command a shell reads.
- eqc reads its arguments from `@<file>`. Reading them moves from the CLI's top-level statements into
  the compiler library, so a test reads them the way eqc does.
- The list of projects holds each project's two paths on a line each, so a path holds any character
  a line can.
- eqc runs bun in the project's folder and hands it each path as an argument of its own, so where
  eqc runs renames no chunk, and a double quote in a folder's name reaches bun.

For a developer: nothing to write, and nothing to look up. A project whose directory or assembly
name holds a `$`, a backtick or a quote, single or double, builds as any other.

It reaches the SDK, eqc and its bundler. The public surface gains `AppBuildOptions.ReadArguments` and
`FromArguments`, which read a command line, and `AppBuildOptions.Watch`. `ModuleBundler.Bundle` takes
the folder bun runs in, a break that nothing outside this repository meets. The developer surface does
not move.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `referenced-projects`: what the SDK hands the build reaches it as it is, whatever characters a path
  or the app's assembly name holds.
