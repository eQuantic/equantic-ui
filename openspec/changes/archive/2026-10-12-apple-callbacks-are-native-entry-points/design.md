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

**A registered method finds its object in the delegate's ivar.** The camera, location and deep links
routed every callback through a process-wide static, so a second camera session took the first one's
frames. `ObjCDelegateClass` defines the runtime class with a pointer-sized ivar, `New(GCHandle)` makes an
instance carrying its context, and a method reads it back from `self` with `Context<T>`: the delegate
object's twin of the block's own storage, one way to find an object for both. Location's and the
deep-link handler's handles live as long as the process, as their objects do. A camera session's
delegate holds its session WEAKLY and the handle is never freed: a frame already on the camera's queue
may read it after `Dispose`, and a freed handle could hand that read whatever took its slot.

**A reply that answers once lets its context go, never its block.** LocalAuthentication and
AVFoundation copy a completion block, call it once, and release it after the call returns, reading
the block's flags to see it is global. Freeing the block from inside its own call, which the biometric
reply did on main, hands that release freed memory, where the allocator may already have written over
the flags. So `ObjCBlock.ReleaseContext` frees the `GCHandle` and clears the field, and the block's two
small allocations stay for the process: the price of an ask, against heap corruption on a background
queue after a successful Face ID. `PendingReply<T>` holds that lifetime for both asks, and its task
runs continuations asynchronously, so nothing awaiting the answer runs inside the native callback.
`ObjCBlock.Context<T>` answers null rather than throwing once the context is gone: an exception
leaving an `[UnmanagedCallersOnly]` method ends the process.

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
