---
applyTo: "src/eQuantic.UI.Native.*/**,src/eQuantic.UI.Sdk.Native/**,tests/eQuantic.UI.Native.Engine.Tests/**"
---
# The native track (Photon)

Photon draws the same components with its own GPU engine on macOS, iOS, Android and Windows. It is
experimental for 1.0: its assemblies carry [Experimental("EQX0001")]. Flag, as a defect:

- An app made to declare a platform artifact (an entitlement, a plist key, a manifest permission). The SDK derives what it can (AOT, hardening) and the app states only its own needs in C#, as builder.Capabilities.UseCamera("why").
- A test of "X follows a change made outside a build" that only checks the result: Photon builds every frame, so the result comes anyway. Assert that the change asks for a build, which is what the web needs.
- A node or behaviour realized on Photon and not on the web, or the other way round, without a sibling case.
- A shader output edited by hand: Sdf.slang is the one source, and scripts/generate-shaders.sh the one writer of the committed outputs.
- A golden as the only proof of a state (pressed, focus ring): put an assertion beside it and compare with the frame without the state.
- A scroll in a test left smooth: ScrollBy glides; tests set SmoothScroll to false.
- A new use of a native assembly outside the native SDK without saying, in its project file, that it means to (EQX0001).
