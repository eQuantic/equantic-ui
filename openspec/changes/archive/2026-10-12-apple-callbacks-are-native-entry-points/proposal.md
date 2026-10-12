# Proposal

Closes #795, a Bug under #774 (Device capabilities on the phones). It is the second of the two defects
the epic's plan puts ahead of everything else on iOS.

## Why

The shared Apple shell turned managed delegates into native function pointers with
`Marshal.GetFunctionPointerForDelegate`, and nothing marked a target as a native entry point:
`ObjCBlock` wrapped any `Delegate` as a block's invoke pointer (network status, the camera's access
request, biometrics with a capturing lambda, motion), and the camera, location and deep links
registered delegate pointers as Objective-C method implementations.

On the Mac and on the simulator the app runs on a JIT, which builds the native-to-managed wrapper on
demand, so all of it works there. An iPhone build is compiled ahead of time with no JIT, and there a
callback from native code must be a static method the compiler saw as an entry point; a delegate to
an instance method or a closure has no wrapper prepared. Five of the eight capabilities the iOS shell
registers would fail at their first callback on a device.

## What Changes

- **A block calls a static entry point and carries its context.** `ObjCBlock(IntPtr invoke, object
  context)` takes the address of a static `[UnmanagedCallersOnly]` method and the object it answers
  for; the block stores a `GCHandle` to that object after its descriptor, where a compiled block keeps
  what it captured, and `ObjCBlock.Context<T>(block)` reads it back inside the entry point. The
  network status, the motion sensor, biometrics and the camera's access request use it.
- **A method registered with the runtime is a static entry point, and finds its object in the
  delegate.** The camera's frame delegate, location's three delegate methods and the deep-link
  AppleEvent handler are defined through `ObjCDelegateClass`: each delegate object carries a `GCHandle`
  to its context in an ivar, and the method reads it from `self`. A second camera session no longer
  takes the first one's frames.
- **A one-shot reply no longer frees memory a framework still holds.** The biometric reply freed its
  block from inside its own call, before LocalAuthentication released it. A reply now lets only its
  context go (`ObjCBlock.ReleaseContext`) and keeps the block, through one `PendingReply<T>` shared by
  the biometric reply and the camera's access request, which leaked its context before. The motion
  sensor reuses its block across starts instead of making a new one each time.

For a developer: nothing to write and nothing to change. The camera, location, biometrics, motion and
network status answer on a device as they do on the simulator.

## Capabilities

### New Capabilities

- `native-callbacks`: what a shell hands native code to call back, and how a callback finds the object
  it answers for.

### Modified Capabilities

None.

## Impact

- **The shared Apple shell** (`eQuantic.UI.Native.Shell.Apple`): `ObjCBlock`, `AppleNetworkStatus`,
  `AppleMotionSensor`, `AppleBiometry`, `AppleCamera`, `AppleLocation`, `AppleDeepLinks`. The iOS
  shell had no callback of its own to change; the macOS shell's own (window, text input,
  accessibility) are outside this change, since a Mac app runs on the JIT or NativeAOT, both of which
  prepare a delegate's wrapper.
- **Public surface moves.** `ObjCBlock(Delegate)` goes (`*REMOVED*`); `ObjCBlock(IntPtr, object)`,
  `ObjCBlock.Context<T>(IntPtr)`, `ObjCBlock.ReleaseContext()` and `ObjCDelegateClass` arrive. The developer surface does not move.
- **Migration**: code that built an `ObjCBlock` from a delegate passes a static `[UnmanagedCallersOnly]`
  method's address and the object it needs, and reads that object with `ObjCBlock.Context<T>`.
