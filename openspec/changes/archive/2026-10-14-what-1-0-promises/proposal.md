# Proposal

Closes #899, #324 and #323, three Tasks under #898 (1.0: the web SDK stable, with the native track
experimental).

## Why

1.0 promises that an app written against it keeps compiling and keeps answering the same through the
1.x line. A promise needs three things it does not have yet: the assemblies it covers, a mark on the
assemblies it does not cover, and the rule that turns from the preview's into the stable one on the
day of the cut.

Measured on main at 0.2.0-preview.63: 39 packages ship an assembly. 12 are the native track's, 2 are
tools of the SDK (the compiler and the code writers, whose package the site's playground references),
and 25 are what an app compiles against, twelve of them icon packs. The one break in C# signatures of
0.2.0-preview.63 was the compiler's `QueueStackStrategy`, which no app calls. Without a list, that
reads as a break of the SDK.

The rule of the preview, that no member survives to keep an old shape alive, is held by
`NoOldShapeSurvivesTests`. On the day 1.0 is cut it has to invert, and a rule that inverts by memory
on a release day is the one that does not.

## What Changes

- Every shipping project has a surface class: `contract`, `tool` or `experimental`. It is derived in
  `Directory.Build.targets` from the project's name (the native projects are experimental, the
  compiler and the code writers are tools, every other one is contract), a project may set it, and
  it is written into the assembly as `[assembly: AssemblyMetadata("eQuantic.Surface", …)]`.
- An experimental assembly carries `[assembly: Experimental("EQX0001", UrlFormat = …)]`, .NET's own
  mark since .NET 8. An app built with `eQuantic.UI.Sdk.Native` does not see it, since choosing that
  SDK is the opt-in, and its publish says once that the native track is experimental in 1.0. Any other
  project that uses a native assembly fails with EQX0001 until it suppresses it.
- A tool's package says it is a tool of the SDK, outside the compatibility promise.
- The rule follows the version in `Directory.Build.props`. While it carries a prerelease label, the
  preview's rule holds as today. Without one, a build of a contract project whose
  `PublicAPI.Unshipped.txt` holds a `*REMOVED*` line fails, an `[Obsolete]` member carries an `EQO`
  diagnostic id, a URL and the major it leaves in, and a contract line of the developer surface
  cannot leave its baseline.
- `CLAUDE.md` says when the preview's rule inverts and what holds it, and its Version Management
  names the three classes. The wiki's Photon page says the native track is experimental in 1.0, in
  the two languages it is written in.

For a developer who uses the SDK: a Photon app built with the native SDK sees one line when it is
published, and nothing else. A web app that references a native assembly by accident is told so, with
EQX0001. Nothing else changes before 1.0. No public signature moves. The developer surface gains no
property, setting or template parameter.

## Capabilities

### New Capabilities

- `release-contract`: what a release promises and to which assemblies, the mark on the ones it does
  not cover, and the rule that follows the version.

### Modified Capabilities

None.
