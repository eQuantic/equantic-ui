# Spec Delta

## ADDED Requirements

### Requirement: A program the build starts receives its arguments as they are

The SDKs SHALL start every program a build of an app needs with a list of arguments, each reaching
the program as MSBuild evaluated it, whatever characters it holds: a dollar sign, a backtick, single
and double quotes, a percent sign, an ampersand, a semicolon, a wildcard, spaces, and a backslash at
its end. No shell SHALL read a path or a value on the way.

#### Scenario: The package folder under a directory whose name holds a dollar sign

- **WHEN** an app is restored with its NuGet package folder under a directory named `nuget $b`, and
  built
- **THEN** the SDK's programs start, bun made executable and eqc run, and the build succeeds

#### Scenario: Arguments with every character a shell reads

- **WHEN** the task that starts a program is handed `a $b`, a value in backticks, `|d; 'e' "f"`,
  `%g%`, `*h?`, `@i`, a value that ends with a backslash, and an empty value
- **THEN** the program receives each of them as one argument, as it was written

#### Scenario: The shipped targets start no shell

- **WHEN** the MSBuild files the two SDK packages ship are read
- **THEN** none of them holds an `Exec` task

#### Scenario: The title, the signing identity and the notary's password

- **WHEN** a macOS app titled `Cash$Flow & Co` is published with the signing identity
  `Developer ID Application: A $b (T)` and a notary password holding `$` and `"`
- **THEN** the icon tool is handed the title as one argument, `codesign` the identity as one, and
  `notarytool` the password as one, and the build does not echo the command line that carries the
  password

### Requirement: A program that fails says which it was

A program the build starts that ends with an exit code other than zero SHALL fail the build, but for
the one step the build does without: the description of the commit a source tree is at, which asks
git and goes on with no answer. When the program reported no error of its own, the build SHALL report
EQ4004 naming the program and the arguments it was started with. The arguments SHALL be left out for
a call whose command line is not echoed, the one that hands `notarytool` its password.

#### Scenario: A program that fails in silence

- **WHEN** a program the build starts writes nothing and ends with exit code 2
- **THEN** the build fails with EQ4004, which names the program, its arguments and the code

#### Scenario: A source tree described with no git

- **WHEN** an app outside the repository builds against its source tree, and git is not on the machine
  or answers with an error
- **THEN** the build goes on, and names the tree's branch alone

#### Scenario: The notary's call that fails

- **WHEN** the call that hands `notarytool` its password ends with an exit code other than zero and no
  error of its own
- **THEN** EQ4004 names the program and none of its arguments

### Requirement: A program runs in the project's folder

The SDKs SHALL start a program in the project's folder unless a step names another, and a folder a
step names by a relative path SHALL be read from the project's, wherever the build was started and
however the task that starts the program is loaded.

#### Scenario: A build started outside the app's folder

- **WHEN** MSBuild is started in another folder on an app that declares a `BunPackage`, whose obj is
  written `obj/`
- **THEN** `bun add` runs in the app's own `obj/eQuantic/npm`, and `node_modules` links into it

### Requirement: Every npm package an app declares is installed

The SDK SHALL hand bun every `BunPackage` an app declares, one `bun add` a package, the first time
the packages are installed.

#### Scenario: An app that declares two packages

- **WHEN** an app declares `dayjs` and `marked` as `BunPackage` items and is built for the first time
- **THEN** `bun add` runs for `dayjs` and then for `marked`

### Requirement: The npm packages' link is made without a shell

When an app declares a `BunPackage`, the SDK SHALL make `node_modules` in the project's folder a link
to the folder the packages are installed in, through the system's own call, on every system.
Something already at that path SHALL stay as it is.

#### Scenario: The link, and what was there before it

- **WHEN** the link is asked for where nothing is, and again where it already is
- **THEN** the first makes it, leading to the installed packages, and the second leaves it as it was
