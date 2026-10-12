# native-callbacks Specification

## Purpose
What a native shell hands the platform to call back, a block's function or a method registered with
the Objective-C runtime, and how that callback finds the object it answers for, so that the same code
is right under the JIT, Mono's ahead-of-time compiler and NativeAOT.

## Requirements

### Requirement: A callback handed to native code is a static entry point

The shared Apple shell and the iOS shell SHALL hand native code only the address of a static method
marked `[UnmanagedCallersOnly]`, for a block's invoke function and for a method registered with the
Objective-C runtime alike, and SHALL NOT turn a delegate into a function pointer.

#### Scenario: The guard reads the shells

- **WHEN** the shared Apple shell or the iOS shell calls `Marshal.GetFunctionPointerForDelegate`
- **THEN** `AppleCallbackTests.NoDelegateBecomesAFunctionPointer` fails, naming the file

#### Scenario: A registered method answers through the runtime

- **WHEN** an AppleEvent carrying a URL is sent to the deep-link handler class through `objc_msgSend`
- **THEN** the URL reaches the deep-link relay's subscribers

### Requirement: A registered method finds its object in the delegate it was called on

A method registered with the Objective-C runtime SHALL find the object it answers for in a handle
carried by the delegate object it was called on, so that two delegates of one class answer each for
their own object.

#### Scenario: Two delegates of one class

- **WHEN** two instances of one runtime class, made with different contexts, are each sent the class's
  method through `objc_msgSend`
- **THEN** each call reaches only the context of the instance it was sent to

### Requirement: A block finds the object it answers for in its own storage

A block built by the shell SHALL carry a handle to its context after its descriptor, keep that context
alive until the block is disposed, and give it back to its entry point from the block pointer the
entry point receives.

#### Scenario: The runtime calls the block

- **WHEN** `enumerateObjectsUsingBlock:` is sent to an array of three items with a block
- **THEN** the block's entry point is called three times, with each item and its index, and records
  them in the block's context

#### Scenario: Two blocks share one entry point

- **WHEN** two blocks with the same entry point and different contexts are each enumerated over their
  own array
- **THEN** each context holds only what its own array delivered

#### Scenario: A framework copies and releases the block

- **WHEN** a block is copied with `_Block_copy`, called, released with `_Block_release` and called again
- **THEN** the copy is the same pointer, and both calls reach the entry point with the same context

#### Scenario: A reply that answers once

- **WHEN** a block held by `_Block_copy` is called, lets its context go from its entry point's answer,
  and is then released with `_Block_release` and called again
- **THEN** the release finds a block rather than freed memory, the context reads as null, and the later
  call does nothing
