# Design

## How Flutter does it

Flutter's tool owns the whole loop: `flutter run` watches the sources, recompiles the changed
libraries into a kernel delta and pushes it into the running VM, which reassembles the widget tree
with its state (no row of `docs/FLUTTER-PARITY.md` covers it yet). The tool knows it is watching
because it is the watcher. Here .NET's `dotnet watch` is the watcher and the SDK is a guest in its loop, so the SDK asks
the mark .NET leaves on the app it runs (`DOTNET_WATCH=1`, documented for exactly this) rather than
inventing a switch the developer would have to set.

## The decisions

- **One decision, three readers.** Whether the app rebuilds its modules is computed once
  (`UIOptions.HotReloads`) and read by the stream, the module cache and the client. Each reader
  computing its own from the environment is how the client listened to a stream the server had not
  mapped (or ignored one it had) as soon as `HotReload` was set.
- **The mechanism .NET already has.** `dotnet watch` ignores a change that matches
  `DefaultItemExcludes`, the same property that keeps `bin` and `obj` out of every default glob. The
  output folder is build output, so it is declared there, once, and the `Content Remove` that did
  half the job goes: the publish's pre-publish generated content honours the same property, which is
  the door the maps came in by (#352).
- **Write in place, prune after.** A measured eqc run rewrites every file it writes, the unchanged ones
  included, so a file older than its start is one it did not write. The prune is the one the generated
  sources already have (#253), with the same two-second margin, and the runtime, which another target
  writes with its source's time, is never a candidate.
- **The build is the reference.** In a workspace, "has syntax" and "is the app's own" part ways. The
  generators ask the compilation which declarations it holds, so a workspace and a build describe the
  same app; a test compiles one app against a referenced project passed both ways and compares.

## What it does not fix

The crash itself is `dotnet watch`'s (dotnet/sdk#55335): any file added to a watched project stops it,
a developer's own new file included. The SDK stops being the one adding them.
