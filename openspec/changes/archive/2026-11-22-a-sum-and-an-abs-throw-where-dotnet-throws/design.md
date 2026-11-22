# Design

## Where a sum is added

`Average` went through a runtime loop in #719 (`$eq.linq.average`), told the type of the values and whether they
are nullable. Writing `Sum` the same way was the first design, and it was measured before it was kept: on a
million doubles, with the loop called from sites with a selector and without, as a page calls it, it took 4.4 ms
where the inline reduce took 0.9 ms. The issue asks for no cost on a sum of reals, so the reduce stays inline and
only the addition changes with the type:

| Values | Addition | Seed |
|---|---|---|
| `int` | `$eq.num.addInt($a, $b)` | `0` |
| `long` | `$eq.num.addLong($a, $b)` | `0n` |
| `decimal` | `$a.add($b)` | `$eq.num.dec(0)` |
| `double`, `float` | `$a + $b` | `0` (a float's total rounded once, at the end) |

A nullable's element is `?? zero`, which leaves the total as it was, evaluated once.

The addition is a function of its own rather than the runtime's `checked`, which reads the width and the value's
type at every element and builds a long's bounds as BigInts at each call. Measured on a million elements, each
in its own process:

| | ints | longs |
|---|---|---|
| unchecked reduce (before) | 0.92 ms | 7.99 ms |
| `checked(a + b, width)` | 2.53 ms | 58.86 ms |
| `addInt` / `addLong` | 0.65 ms | 9.30 ms |

## What decides the type

`LinqAccumulation.ValuesOf` reads it from the bound overload, as `Average` already did: the selector's result
type, or the source's element type. The call's answer, which the reduce read before, says `long?` for a
nullable long's sum, and the reduce added a null to a `0n` seed. With no model the call's answer is still the
guess, as before.

## What is left

On an array or a list of at least four vectors' worth of ints or longs, .NET adds in vector lanes, and a total
that leaves the range in one order and comes back in another throws on one side only. Measured on this
machine's 4-lane vectors: 64 ints with `1` and `int.MaxValue` in different lanes and `-1` in the first lane
answer `int.MaxValue` in .NET, where any sequence .NET adds one element at a time throws, and so does the
browser. Which elements share a lane depends on the machine's vector width, so .NET's own answer to such a
sequence depends on the machine. It is written down in the runtime's `addInt`, not listed as a conformance
divergence, since a row would pass or fail by the host's vectors.
