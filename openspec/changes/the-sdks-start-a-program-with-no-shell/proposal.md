# Proposal

Closes #841, a Bug under #849 (The transpiler's fences hold on every path, part three), the feature
that continues #565. #836 took the project's values out of eqc's command, and a review of that change
found that the rest of the class is every other program the SDKs start.

## Why

Every program the two SDKs start goes through MSBuild's `Exec`, which writes the command into a
script that `/bin/sh` runs on macOS and Linux and `cmd.exe` on Windows. Each shell reads the command
before the program does. Measured on macOS, with the NuGet package folder under a folder named
`nuget $b`, a build stops at the first program it starts, `chmod +x` on bun's own path (MSB3073),
before eqc runs. A `$` or a backtick inside double quotes is read by `/bin/sh`, a `%` by `cmd.exe`,
and a value that ends with a backslash closes its own quotes: an `Exec` handed the title `A\B and C\`
failed with "unexpected EOF while looking for matching". The app's title, the signing identity and
the notary's password are text a developer or a CI writes, and they reach the icon tool, `codesign`
and `notarytool` through the same shell.

## What Changes

- The SDKs gain a task assembly, `eQuantic.UI.Sdk.Tasks`, shipped inside both SDK packages, with two
  tasks. `RunProgram` starts a program with a list of arguments and no shell. `LinkDirectory` makes the
  `node_modules` link a `BunPackage` needs through the system's own call, where `mklink /D` was a
  command of `cmd.exe`.
- Every `Exec` of the two SDKs becomes one of them: 8 in the web SDK, 18 in the native SDK. An
  argument is an item whose metadata holds the value, so MSBuild neither splits it at a semicolon nor
  expands a wildcard in it.
- eqc is started in the project's folder again, with its response file named by its full path. #836
  ran it in the response file's folder only to keep that path away from the shell.
- Every `BunPackage` an app declares is installed. Only the first one was: the install ran in batches
  under the condition "no `node_modules` yet", which the first `bun add` made false for the rest.
- A program that fails without reporting an error of its own fails the build with EQ4004, naming the
  program and the arguments it was started with, where `Exec` named its command in MSB3073.
- The call that hands `notarytool` the notary's password no longer writes its command line to the
  build log.

For a developer: nothing to write. An app whose folders, package folder, title or signing identity
hold a `$`, a backtick, quotes, a `%`, an `&` or a trailing backslash builds as any other.

It reaches the two SDKs and the new assembly they ship. eqc, eqicon and the runtime do not change. The
developer surface does not move, and the public surface does not either: the tasks assembly ships
inside the SDK packages, as eqc and eqicon do, and is no package of its own.

It does not reach the repository's own build: the Server's `BundleRuntime` and the runtime's tests
still start bun through `Exec`, which only a clone of the framework runs. That is a follow-up of its
own.

## Capabilities

### New Capabilities

- `build-programs`: every program a build of an app starts receives its arguments as they are, and
  one that fails says which it was.

### Modified Capabilities

None.
