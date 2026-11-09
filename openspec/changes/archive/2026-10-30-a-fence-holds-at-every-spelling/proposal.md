## Why

Closes #943, #951, #953 and #1148, bugs under #849 (the transpiler's fences hold on every path, part
three). Each is a place where what the build refuses depended on how the code was spelled, or on a path
the fence did not stand on.

- A Server Action was read off the method's attribute lists by name, so an alias of the attribute or the
  defining half of a partial method crossed with its body and got no stub (#1148).
- A `[ServerOnly]` override of `ToString`, `Equals` or `GetHashCode` built on a type that crosses, and the
  browser printed the type's name where .NET runs the override: the runtime calls those on its own, so the
  fence on a call never met them (#943).
- A type test or `as` of a package's type was a null check: `o is Vendor.Status` answered true for any
  value, and `o as Vendor.Status` handed the value back (#951).
- A refused static call on the `string` keyword also reported EQ1001 for the keyword, where `String` said
  one thing (#953).

## What Changes

- A developer marks a Server Action as C# lets them, an alias or a partial method included, and it crosses
  as its stub. The server already found it by reflection.
- A `[ServerOnly]` override of `ToString`, `Equals(object)` or `GetHashCode` on a type that crosses fails the
  build with EQ2010 where it is declared. This replaces what #1146 pinned for a record, whose twin kept its
  synthesized text and hash, and printed what .NET would not. Migration: let the member cross, or mark the
  type `[ServerOnly]`.
- `is`, `as` and a type pattern over a package's type fail the build with EQ2004 naming the type and its
  assembly, as a construction or a static read of it does since #822.
- A refusal on a keyword is told once, as EQ2004. The BCL surface baseline moves ten verdicts from EQ1001 to
  EQ2004.

## Capabilities

### New Capabilities

(none)

### Modified Capabilities

- `server-actions`: gains the requirement "A Server Action is known by its symbol".
- `client-boundary`: gains the requirements "A server-only override of what the runtime calls is refused
  where it is declared" and "A type test of a package's type is refused".

## Impact

- **eqc**: `ComponentParser`, `PlainClassModule`, `TwinOwnMembers`, `PatternConverter`, `CSharpToJsConverter`
  and the three emitters that write a twin.
- **The runtime, the realizers, the Photon shells, the SDKs, the templates**: none.
- **Public surface**: none. The developer surface does not move. Code that built and ran wrong now fails
  its build, with EQ2010 or EQ2004.
