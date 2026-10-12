# Design

## How Flutter solves it

Dart meets the same constraint in its FFI, for the same reason: an ahead-of-time compiled program
cannot make a native trampoline for an arbitrary closure at run time. `Pointer.fromFunction` accepts
only a static or top-level function, which the compiler turns into an entry point, and state reaches it
through what the native side passes back (a context pointer, a port). This change takes the same
shape: a static `[UnmanagedCallersOnly]` method, and the object it answers for carried by the native
side. No `docs/FLUTTER-PARITY.md` row covers interop with the platform, so none is added.

## Decisions

**The context lives in the block, after its descriptor.** A compiled Objective-C block keeps what it
captured right after the descriptor, and the invoke function receives the block as its first argument.
So the block this shell builds by hand gains one pointer-sized field, a `GCHandle` to the context, and
the descriptor's size counts it. The block stays GLOBAL, so a framework's copy hands back the same
pointer and the field is where the entry point looks for it, whoever holds the block.

**The entry point is passed as an address, not a typed function pointer.** Every block has its own
signature, and `ObjCBlock` is one type; `IntPtr` is the common denominator, and the cast at the call
site, `(IntPtr)(delegate* unmanaged<…>)&Method`, is where the compiler checks the signature and refuses
a method without `[UnmanagedCallersOnly]` (CS8786).

**A registered method routes through the static instance it already had.** The camera, location and
deep links each have one instance per process, and their delegate classes already routed to it. Giving
each runtime class an ivar to hold a `GCHandle` would add a second way to find the same object, so the
method is made a static entry point and the routing stays.

**BOOL crosses as a byte.** A `bool` parameter is not blittable, and an `[UnmanagedCallersOnly]`
method takes only blittable types; `success != 0` reads it.

**The instrument fails.** A test reads the shared Apple shell and the iOS shell and fails when either
calls `Marshal.GetFunctionPointerForDelegate`, so a delegate handed to native code is a red build
rather than a crash on the first device that runs it.

## What can be proved here, and what waits for a device

On the Mac the block tests run against the real runtime: `enumerateObjectsUsingBlock:` calls the
entry point once per item, two blocks sharing one entry point each read their own context, and a block
survives `_Block_copy` and `_Block_release`. The deep-link handler is called through `objc_msgSend`
with a real AppleEvent and the URL reaches the relay. Those prove the shape works under the JIT. That
the old shape fails on a device and this one does not is the documented behaviour of Mono's AOT, and
the run that shows it is the physical-device pass, #792.
