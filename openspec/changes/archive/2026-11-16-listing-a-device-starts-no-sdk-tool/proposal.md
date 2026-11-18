# Proposal

Closes #1135, a Bug under #1174.

## Why

A source-tree app could not `dotnet run` on a device. The native SDK wrote the Android icon from an
`InitialTarget`, which runs in every invocation of the project, the Android workload's device listing
(`ComputeAvailableDevices`) and its deploy (`DeployToDevice`) included. Listing a device built the
eqicon edge and started eqicon. On a source tree the SDK loads its tasks through `TaskHostFactory`,
`dotnet run` starts those two invocations with no `DOTNET_HOST_PATH`, the task host could not start,
and the listing and the deploy failed with MSB4221. From packages it was a process started for
nothing on every listing and deploy.

The `InitialTarget` was chosen as the only hook early enough for Android's check that every declared
resource exists. That check is `AndroidComputeResPaths`, in the ordinary target
`_ComputeAndroidResourcePaths`, which a `BeforeTargets` reaches.

## What Changes

- The Android icon is written just before `_ComputeAndroidResourcePaths`, after `ResolveReferences`
  has built the eqicon and task edges, so the target no longer builds the edge itself.
- It runs only when the icon is out of date: its inputs are the icon's source, eqicon and a stamp of
  the arguments eqicon is given, and its outputs are the five mipmaps, touched after a run. A device
  listing never reaches it, and a deploy after a build skips it.
- The Android queries fragment, which the deploy reaches through the manifest merge and which started
  eqicon on every deploy, runs only when the assembly or eqicon changed, read off a stamp of its own, so
  the fragment keeps the time of its last change for the merge.
- The `dotnet run` half (`DOTNET_HOST_PATH` missing for those invocations) is the .NET SDK's, to be
  reported there: a console project with no workload reproduces it.

## Impact

- `src/eQuantic.UI.Sdk.Native/Sdk/Sdk.targets` and `Sdk.props`. An app writes nothing new.
