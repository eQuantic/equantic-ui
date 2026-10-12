# Design

## Context

See proposal.md for the defect. The SDK starts eqc with one `Exec`, whose command MSBuild writes into
a script that `/bin/sh` runs on macOS and Linux and `cmd.exe` on Windows. Every value in that command
is read by that shell first: the source roots, the output folder, three lists' paths, the generated
folder, the bundler's path and, since #827, the assembly name.

Flutter has no counterpart to ask: this is how one .NET build hands another program its inputs.

## Goals / Non-Goals

**Goals:**

- No value of the project is written into a command a shell reads.
- What eqc is handed is read by one function in the compiler library, which a test calls.
- A list's path holds any character a line can.

**Non-Goals:**

- The compiler's own path. It is still the one value in the command, quoted: the SDK's own install
  location, which the developer neither chooses nor names.

## Decisions

**A response file, one argument a line.** It is the mechanism .NET's own tools use (`csc @file`,
`dotnet build @file`), and it has one rule on every system: a line is an argument. The SDK builds an
ordered item list, each value kept whole as item metadata, so a semicolon, a star or a percent sign
in it is neither a separator nor a glob nor an escape, and writes it with `WriteLinesToFile`. The
command is `dotnet "<eqc.dll>" "@<file>"`, the file named by its path under `obj`, relative to the
project's directory, which is the working directory MSBuild sets.

Quoting each value for the shell was set aside: two shells, two rules, and `cmd.exe` reads `%` inside
quotes. An environment variable was set aside for the same reason, as `cmd.exe` reads `%NAME%` as
text. A task of its own would put an assembly in the SDK for one call.

**The target splits where the file is written.** `CompileEQuanticUI` was one target that computed its
inputs, wrote their lists and ran eqc. The inputs, the response file and the run are three targets
now, chained by `DependsOnTargets`, so a test runs the target that writes the response file, as the
SDK ships it, in a real MSBuild.

**eqc reads its command line in the library.** `AppBuildOptions.ReadArguments` expands `@<file>`,
and `AppBuildOptions.FromArguments` turns the arguments into the options the CLI built inline. The
CLI keeps `--watch` and the usage line, and a test reads a response file as eqc reads it.

**Each project's two paths are a line each.** The list was `assembly|project file`, which a bar in a
path cut in the wrong place. Two lines a project need no separator: a line ends where a path cannot
continue.

## Risks / Trade-offs

- A response file that is not there stops eqc with the path it was given, which MSBuild reports as
  the step that failed. The SDK writes it in the target before the one that runs eqc.
- The dashboard sample's output is the measure that nothing else moved: byte for byte what main
  writes.
- A bar is read whole by eqc now, and that is all it buys: the C# compiler does not build a project
  with a reference under a folder whose name holds one (CS0006, measured). The pair format costs
  nothing over the old one, and keeps eqc out of that failure.
