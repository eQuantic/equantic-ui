# Design

## Context

See proposal.md. The preview's rule is a section of `CLAUDE.md` and a guard,
`NoOldShapeSurvivesTests`, which refuses every `[Obsolete]` and a second `Deconstruct` in
`eQuantic.UI.Primitives` and `eQuantic.UI.Components`. The public API is declared per project in
`PublicAPI.Shipped.txt` and `PublicAPI.Unshipped.txt`, for the projects `Directory.Build.targets`
selects, and the developer surface in `developer-surface.baseline.txt`. The version is one line of
`Directory.Build.props`, bumped only at a release.

Flutter shipped 1.0 for mobile alone, with web and desktop stable two and three years later, and it
names a change that breaks code through a deprecation that lasts a release. .NET marks an API that may
change with `ExperimentalAttribute` and a member that will go with `ObsoleteAttribute`, each with a
diagnostic id and a URL.

## Goals / Non-Goals

**Goals:**

- Say which assemblies 1.0 promises, in the assemblies themselves and in one place.
- Mark the native track in a way a compiler reads, with nothing for a Photon app to write.
- Make the stable rule switch on by itself on the day the version loses its prerelease label, and
  fail where it is broken, at the pull request.

**Non-Goals:**

- Cutting 1.0, or deciding when. The rule waits for the version.
- Moving a tool's types to internal. The site's playground compiles C# through the compiler's
  package, and a tool's surface is free to change, so the class says so and nothing is hidden.
- A window for the experimental track. It is not promised, and its removals stay free.

## Decisions

**The class is derived from the name, in one place, and written into the assembly.**
`Directory.Build.targets` already decides which projects declare an API, by one rule shared with
`scripts/public-api.sh`. The class sits beside it: `eQuantic.UI.Native.*` is experimental,
`eQuantic.UI.Compiler` and `eQuantic.UI.Codegen` are tools, and a project may set the property. A new
project is contract by default, the stricter answer. The class is written into the assembly with
`AssemblyMetadataAttribute`, .NET's own key and value, so a test, a tool or a person reads it from
what ships.

**The native track uses `ExperimentalAttribute`, and the native SDK is the opt-in.** It was measured
before it was chosen, on three class libraries: an assembly marked `[Experimental("EQX0001")]` used
by a plain one fails its build with `error EQX0001`, used by another assembly marked with the same id
says nothing, and `NoWarn` lets the plain one through. So the native assemblies mark themselves and
need nothing between them. `eQuantic.UI.Sdk.Native` adds the id to `NoWarn` for the app, since
choosing the native SDK is choosing the native track, and the product principle forbids a line the
developer has to write for it. A web app that references a native assembly by accident is the case
the mark exists for.

**A tool is not marked experimental.** Experimental says the surface may become stable. A tool's
surface never will: it serves the SDK, and the compiler's package is referenced by the site's
playground. The class and the package's description say so, and the stable rule leaves a tool's
removals free, as it does an experimental one's.

**The rule follows the version in `Directory.Build.props`, not a tag.** The version is the release
being prepared, a checkout has it with no history, and it is the line a release bumps. While it
carries a prerelease label, the preview's rule holds, and the next major's previews are where a
removal goes after 1.0. Without one, nothing of the contract leaves. A removal needs an `[Obsolete]`
first, and the line moves to the next major's preview when the removal is due. A rule read from tags
would need the history in every checkout and could not tell a minor being prepared from a major.

**The removal check is a build target, so it fails at the pull request.** A target beside the API
analyzer's reads the version, the class and the project's `PublicAPI.Unshipped.txt`, and stops the
build of a contract project that holds a `*REMOVED*` line at a stable version. It runs on every
host, and the release's `ship` needs no check of its own: the build of the bump fails first. The
target's own cases run it through `dotnet msbuild` on a fixture project, at a stable and at a
prerelease version.

**The obsolete rule and the developer surface read the version from the built assembly.** The
tests read `AssemblyInformationalVersionAttribute` of the assemblies they scan, which carries the
version `Directory.Build.props` gave the build. Each rule is a function of the version and of what
it scans, so its cases run both versions on synthetic input, and the scan calls it with the real one.

## Risks / Trade-offs

- A project in this repository that uses a native assembly without the native SDK fails with
  EQX0001: the Photon design host, and the native test projects. Each says why it suppresses the id.
- The build message of the native SDK shows on every build of a Photon app. It is one line.
- The scan of obsolete members reads the contract assemblies the web tests reference: the
  vocabulary, the components, the charts and the web realizer. The server, the integrations and the
  icon packs are held for removals by the build target, which reads every contract project, and for
  the form of an `[Obsolete]` by the rule in `CLAUDE.md` alone.
- A stable version with a removal waiting cannot build. That is the point, and the message names the
  two ways on.
