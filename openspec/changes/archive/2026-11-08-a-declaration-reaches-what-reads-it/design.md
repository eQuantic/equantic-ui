## Context

Permissions and bundle facts are stated in C#, read by a generator into assembly attributes, and read by `eqicon` out
of the assembly's metadata into each platform's file.

## How Flutter answers it

Flutter leaves the manifests to the developer, and a plugin's README says what to add. The product principle rules
that out: here the SDK writes the file, so it owes the file only what is true.

## Decisions

- **Refuse at the build, not by the absence of a method alone.** The general form `Use(DeviceCapability.X, …)` reaches
  the same permission, so the generator refuses it too, and the mapping tables lose the four. Each comes back with the
  API that decides what it asks of each platform, which `PhotoLibraryAdd` shows: `WRITE_EXTERNAL_STORAGE` has no
  effect from Android 11.
- **Which facts a phone reads is a list of what it does not.** An Apple key an app names through `Key` is one the SDK
  cannot know, and the next one Apple adds should work, so what is left out is named: the three keys the typed
  builder methods write for a Mac.
- **A message, never a warning.** One `Program.cs` often serves a Mac head and a phone head, so a Mac fact is normal
  there, and a warning would fail a build that treats warnings as errors.

## Risks / Trade-offs

- A key that only a Mac reads, stated through `Key`, reaches the phone's manifest, where it does nothing.
