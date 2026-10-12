# Design

## Context

See proposal.md for the defect. The two SDKs run 26 `Exec` tasks: eqc, eqicon (vectors, icons,
entitlements, the macOS bundle, the capability manifest), bun (`chmod +x`, `bun add`), the
`node_modules` link (`ln -s` behind a shell test, `mklink /D`), `git describe` in a source tree, and on
macOS `chmod`, `codesign`, `ditto`, `notarytool`, `stapler` and `hdiutil`. MSBuild runs each through
`/bin/sh` or `cmd.exe`.

Flutter's tool starts every process with `Process.start(executable, arguments)`, whose `runInShell`
is off unless asked for: a program and its list of arguments, never a command for a shell to read.
docs/FLUTTER-PARITY.md has no row for build tooling, and this is the same answer in .NET's terms.

## Goals / Non-Goals

**Goals:**

- No program the SDKs start is reached through a shell, on any system.
- One mechanism for all of them, and a guard that fails when an `Exec` comes back.
- What each program receives is the same list of arguments it received before, for every value a
  shell did not mangle.

**Non-Goals:**

- The repository's own build: the Server's `BundleRuntime` and the runtime's tests run bun through
  `Exec`, and only a clone of the framework runs them. A follow-up.
- Moving work out of MSBuild into the tools (eqicon running `codesign`, eqc running `bun add`). Each
  call keeps its program; only the way it is started changes.

## Decisions

**A task assembly the SDKs ship, built against netstandard2.0.** MSBuild has no task that starts a
program with a list of arguments: `Exec` is the only general one, and it is a shell. So the SDKs take
one of their own, which is what .NET's own SDKs do (`Microsoft.NET.Build.Tasks`). netstandard2.0 is
what every MSBuild loads in its own process, the .NET SDK's and the .NET Framework one Visual Studio
builds with, so one assembly serves both. An inline task (`RoslynCodeTaskFactory`) was set aside: it
compiles C# held in XML on every developer's machine, in every new build process, and nothing tests it
before that. An environment variable expanded by the shell (`"$EQ_TOOL"`) was set aside as #836 set it
aside: two shells, two rules, and `cmd.exe` reads `%` and `&` in what it expands.

**`RunProgram` derives from `ToolTask`.** ToolTask is MSBuild's mechanism for running a tool: it
starts the process without a shell, reads both streams, logs a line in the canonical
`file(line,col): error CODE: text` shape as an error and any other as a message, kills the process
tree when the build is cancelled, and takes `EnvironmentVariables` and `EchoOff`. The task adds the
program, the arguments, the folder, `IgnoreExitCode` and `ConsoleToMSBuild`, named as `Exec` names
them so a call site reads the same. Its output is logged at high importance, as `Exec` logs it.

**One command line that splits back into exactly the arguments.** `ProcessStartInfo.ArgumentList` is
not in netstandard2.0, so the task writes the command line itself, by the convention every Windows
program splits its command line with and that .NET applies to a command line on macOS and Linux:
quotes around an argument that holds a space or a quote, a quote inside escaped, and the backslashes
before a quote doubled. It is what .NET writes for an `ArgumentList` on Windows.

ToolTask turns every backslash of the command line into a slash on macOS and Linux
(`AdjustCommandsForOperatingSystem`), taking it for a Windows path: measured, a title `A\B` reached the
program as `A/B`. The task returns the line as it wrote it.

**An argument is an item, and its value is metadata.** `Option="--sign" Value="$(Identity)"` is two
arguments, `Value="--force"` one, and a value is passed even when it is empty, as a quoted `"$(X)"` was.
A property's value in an item's `Include` would be split at a semicolon and expanded as a wildcard,
and in a list-typed parameter split too; in metadata it is neither, which #836 measured for eqc's
response file. The item's name is a label for the reader. An option repeated per item
(`--exclude <path>` for each other build of the app) is one item per path, batched.

**A source tree loads the task in a task host.** A package's assembly is loaded once and never
changes. A source tree builds the assembly in the same graph that uses it, so it is a graph edge like
eqc and eqicon, and its `UsingTask` names `TaskHostFactory`: a task host that ends with the build, so
a rebuilt assembly is the one that runs and a reused node never holds the file. Measured on the
probe: 0.4 s for the whole run either way. One property names the factory, MSBuild's own default for
a package, so each task is declared once.

**EQ4004 for a program that fails in silence.** A program that reported its own errors has said what
went wrong, as eqc does. One that did not gets the command it was started with, as `Exec`'s MSB3073
gave it. A program that cannot be found is MSBuild's own MSB6003.

**eqc back in the project's folder.** #836 ran eqc in the folder of its response file and named the
file by its name alone, because a path in the command was read by the shell. With no shell, the file
is named by its full path and eqc runs where it ran before #836. bun's folder is named by the bundler,
so neither move renames a chunk.

**The link through the system's call.** `LinkDirectory` calls `symlink` on macOS and Linux and
`CreateSymbolicLinkW` on Windows, with the flag that lets Developer Mode make one without elevation.
Something already at the path stays, which the script's `[ ! -L node_modules ]` meant and did not
quite do: on a real folder `ln -s` made a link inside it. A link that cannot be made is said at high
importance and does not fail the build, as the script's ignored exit code did not.

**The packages to install are decided once.** The install batched one `Exec` over the packages under
the condition "no `node_modules` yet", and MSBuild evaluates a task's condition again before each
batch: the first `bun add` made the folder, and the second package was never added (measured on main
with a bun that makes the folder, as bun does). The list of what to add is built under that condition
once, and the task then runs for every package of the list.

**`git describe` stays tolerant.** In a source tree outside the repository the SDK names the commit it
builds against, and no git, a tarball or a shallow copy must not fail the build. `IgnoreExitCode`
covers a program that is not on the machine at all, which the shell answered with 127.

**The notary's password is not echoed.** The call that carries it sets `EchoOff`, and its command
line, which was in every log at normal verbosity, is not written. ToolTask logs a command line
whatever `EchoOff` says, since it is `Exec` that asks first, so the task asks, and EQ4004 names no
argument of such a call. What this does not reach: at diagnostic verbosity, and so in a binary log,
MSBuild records every task's parameters, these arguments among them, as it recorded `Exec`'s command
(measured).

## Risks / Trade-offs

- A package's task assembly is loaded by long-lived MSBuild nodes and, on Windows, cannot be deleted
  while they live: clearing the package cache between two packs of one version needs
  `dotnet build-server shutdown` first. Every SDK that ships a task shares this.
- A symbolic link on Windows needs Developer Mode or elevation, as `mklink /D` did.
- The repository's own build keeps its `Exec` tasks until the follow-up.
