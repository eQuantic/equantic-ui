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

- The compiler's own path. It is still the one value in the command, quoted, and a shell still reads
  it: the SDK's package folder, the source tree, or the `EqcCliPath` a project states. So does every
  program the two SDKs start, and with the package folder under a folder named `nuget $b` a build
  stops at `chmod +x` on bun's own path before eqc starts (MSB3073, measured). Starting a program with
  an argument list, for every one of them, is #841's.

## Decisions

**A response file, one argument a line.** It is the mechanism .NET's own tools use (`csc @file`,
`dotnet build @file`), and it has one rule on every system: a line is an argument. The SDK builds an
ordered item list, each value kept whole as item metadata, so a semicolon, a star or a percent sign
in it is neither a separator nor a glob nor an escape, and writes it with `WriteLinesToFile`. The
command is `dotnet "<eqc.dll>" @equantic.eqc.rsp`, run in the folder that holds the file: MSBuild sets
a process's working directory itself, so the file is named by its name alone and no path of the
project is in the command. Named by a path relative to the project, the file still carried what the
obj folder's path holds, a Configuration, a target framework, an artifacts layout's folders (found in
review). Every path in the file is absolute, so eqc reads none of them from where it runs.
bun did read it: it names a shared chunk by a hash of its modules' paths from the folder it runs in,
and it inherited eqc's, so every chunk was renamed (the dashboard's comparison with main found it).
The bundler names bun's folder now, the project's, where the build always ran it.
And it hands bun each path as an argument of its own: a command line of quoted paths lost every
double quote a folder's name held, and .NET builds a project under such a folder.

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
- A double quote reaches eqc and bun whole, and an app under such a folder builds and runs. Its
  publish stops at ASP.NET Core's own compression of static web assets, as a `dotnet new web` app's
  does there (MSB6006, measured).
