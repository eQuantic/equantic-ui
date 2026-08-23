# Fase 6 — deriving the mapping

## Why

Fase 5 answered the SEMANTIC half of "how do we cover more C# without writing it one construct at
a time": the five hand-written rules collapsed into one conversion table read off the bound tree,
and a conversion is now honoured at every site C# applies it rather than the sites a strategy
remembered. It did not reduce the strategy COUNT, and it was never going to. What is left is not
semantics but MAPPING — which JavaScript shape a given C# call becomes — and that is where
"one by one" still bites.

Where it started, measured at 69acdc17 + slice 7 in flight, and where it stands now. Re-measure
before quoting either column — the numbers are what the baselines say today, not what this
paragraph remembers:

| | At the start | Today | Concentration now |
|---|---|---|---|
| Strategies still emitting text (no writer guarantees) | 111 | **92** | 26 Expressions, 23 LINQ, 21 Types, 8 Primitives, 6 Invocation, 4 Special, 3 UI, 1 Async |
| BCL members fenced (a build error today) | 99 | **32** | 23 EQ2004 (extension methods declared outside the compilation), 6 EQ1001, 3 EQ1004 |
| Conversion divergences | 2 | **7** | the two `unchecked` limits, plus five found by widening the instrument: array index out of range (×2) and `m[k]++`/`++m[k]`/`--m[k]` on a missing key |

The divergence row went UP, and that is the phase working rather than failing. Two of them were
all the generator could see; the other five were always there, in a grammar that had never written
an array index or a compound assignment through a dictionary key.

Both remaining pockets are CONCENTRATED and REGULAR, which is what makes them derivable rather
than writable. That is the whole thesis of this phase.

## Slices

| # | Slice | Why it is derivable | State |
|---|---|---|---|
| 1 | **LINQ as one mechanism.** DONE. Instrumented first, and the instrument said stop: of the 57 operators the 42 strategies claimed, 14 had no executed case and 23 had one. Closing that found ELEVEN bugs before a line was refactored — the OrDefault family answering null where .NET answers 0, an enum field rendering nothing, Zip walking the longer sequence into NaN, Take(-1) dropping the last element, SequenceEqual emitting a name that exists nowhere. Then the table: LinqSurfaceTailStrategy already WAS one, so it grew rather than gaining a rival, and 19 strategies moved in across three batches (43 files → 24, 2474 lines → 1717, baseline 111 → 92) with every pin byte-identical. What stays has a reason, not a shape: Reverse alone means two different things depending on whether the receiver is a List or a sequence | done |
| 2 | **The numeric BCL as a table.** Method symbol to a JS expression template, one generated conformance case per entry | The 71 EQ1001 members are PURE FUNCTIONS with a 1:1 mapping (`Double.AcosPi(x)` is `Math.acos(x) / Math.PI`). No refactor, pure addition, and every entry is independent of every other — the one slice in this plan that parallelises cleanly | done — 71 → 6 fences (Int128 BigMul, the two Unicode-table members, the intern pool, the two PLATFORM estimates — .NET on ARM64 answers FRECPE, a number this side cannot produce). 65 entries in PrimitiveStaticStrategy's tables + `$eq.bits` (rotations, counts) + `$eq.math` (min/max tie rules, FMA by TwoProduct, bit-adjacent doubles, and the *Pi family PORTED LITERALLY from dotnet/runtime's aocl-libm polynomials — .NET never calls the platform libm for those, so the only bit-exact answer is running the same reduction into the same coefficients; probing found TanPi(0.25) is 0.9999999999999999 THERE too). 135 conformance cases, magnitude ties, ±0, parity infinities and residue-error paths included |
| 3 | **Widen the differential generator** to LINQ chains and statements. DONE. Its grammar now writes `long`, `decimal`, `char`, `enum`, nullables, records, patterns, dictionaries, `DateTime`, `try`/`catch` and LINQ chains, none of which it had ever produced | After 1 and 2 there is a large new surface, and the generator is the only instrument that finds what nobody thought to test. Its grammar is still expression-heavy | done — and the lesson is the phase's most important one: a DRY instrument and an EXHAUSTED one look identical from outside. It had returned 0 divergences over 3600 programs, which read as coverage and was silence. Widening the grammar took the gap baseline from 2 to 7 in one afternoon |
| 4 | **Component conformance.** DONE. Components are lowered by `WebRealizer.Lower` through a style sink into a canonical JSON fixture and replayed by the vitest twin through `lowerVisualNode` — tag, attributes, event NAMES and children, with the atomic class names pinning the style hash across both sides. A case can also be DRIVEN: a press is the index of a click handler in document order, invoked between frames, so a Select that opens and an Accordion that switches section are compared after the state moved, not only before. A guard asserts both sides cover the same names. Two things are deliberately not compared, and the code says why: the anchored panel's generated ID (each side hashes different inputs; an open panel never comes from SSR, so they never meet) and class ORDER (attribute order does not enter the cascade). Instead every ARIA reference is asserted to RESOLVE to an id the tree has, on the un-normalised tree — the check a pin against the attribute itself cannot make. First runs found the `MathF.Round` mirror (every small button's label 16.5px in the browser, 16px from the server) and the class-order difference now documented | done |

## When this phase is DONE

A plan without a stop condition chases an ideal forever. Three numbers end it:

1. `ir-migration.baseline.txt` reaches ~20 — only genuinely irregular constructs still emit text.
   **At 92.** The 23 LINQ entries left are call-shaped on purpose; the 26 in `Expressions` and 21
   in `Types` are the honest remainder, and the number has not moved since slice 1.
2. `bcl-surface.baseline.txt` fences only what is impossible BY CONSTRUCTION (`System.IO`,
   reflection, threading), not what is merely unwritten. **At 32, and close.** The 6 EQ1001 are
   deliberate (`Int128.BigMul`, two Unicode-table members, the intern pool, two platform estimates
   .NET answers from ARM64 hardware). The 23 EQ2004 are one shape, not 23: an extension method
   whose declaring class is outside the compilation.
3. The differential generator runs N seeds clean across expressions, statements and LINQ.
   **Not yet, and the bar moved.** Clean means clean on a grammar that writes the language, and
   each widening has produced findings. The condition should be read as "widening the grammar
   stops yielding divergences", which is a stronger claim than any seed count.

   **First reading against that bar (2026-08-23).** Two widenings in one sitting, covering
   everything the grammar had never written on the numeric and text side: the narrow integers
   (byte/sbyte/short/ushort, and uint's wrap at 2^32), `ulong` as a BigInt, `double` and `float`
   with the store-rounding rule, every bitwise operator, the exact members of `Math`, the ordinal
   string surface (`IndexOf`, `LastIndexOf`, `Split`, `Contains`, `Insert`, `TrimEnd`,
   `ToLowerInvariant`), and index-from-end and ranges over both arrays and strings. **3000
   programs, zero divergences.** The one failure was the generator testing ITSELF — `~` and `>>>`
   put a value at 2^31 from small operands, and the fold then reached the default-context int
   overflow this instrument exists not to hunt.

   That is the first widening in this arc to yield nothing, which is what the condition asks for
   — but on ONE axis. What the grammar still does not write: tuples, structs and user-defined
   operators, `checked`/`unchecked` blocks, labelled break/continue, local functions, `HashSet`,
   `StringBuilder`, and the whole async surface. The condition holds when a widening on an axis
   nobody has walked yet also comes back empty.

When the three hold, the compiler is finished in the sense that matters, and the effort moves to
PERFORMANCE — which this whole arc has never once measured.

## Ordering note

(2) is cheap, low-risk and independent entry by entry; (1) touches working code and needs the net
tight before it starts. With two sessions on one repository they are the natural split, and they
do not overlap in a single file.
