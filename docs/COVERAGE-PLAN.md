# Fase 6 — deriving the mapping

## Why

Fase 5 answered the SEMANTIC half of "how do we cover more C# without writing it one construct at
a time": the five hand-written rules collapsed into one conversion table read off the bound tree,
and a conversion is now honoured at every site C# applies it rather than the sites a strategy
remembered. It did not reduce the strategy COUNT, and it was never going to. What is left is not
semantics but MAPPING — which JavaScript shape a given C# call becomes — and that is where
"one by one" still bites.

Measured at 69acdc17 + slice 7 in flight:

| | Count | Concentration |
|---|---|---|
| Strategies still emitting text (no writer guarantees) | 111 | **42 LINQ**, 26 Expressions, 21 Types, 8 Primitives, 6 Invocation, 4 Special, 3 UI, 1 Async |
| BCL members fenced (a build error today) | 99 | **71 EQ1001** — of which **42 `Double`**, 12 `Int64`, 12 `Int32`, 4 `Char`, 1 `String`; plus 23 EQ2004, 3 EQ1004 |
| Conversion divergences | 2 | the documented `unchecked` limits |

Both remaining pockets are CONCENTRATED and REGULAR, which is what makes them derivable rather
than writable. That is the whole thesis of this phase.

## Slices

| # | Slice | Why it is derivable | State |
|---|---|---|---|
| 1 | **LINQ as one mechanism.** One table from operator symbol to emit shape, over the existing IR, replacing ~42 strategies | The surface is closed and uniform — source plus lambda(s) to an array/iterator operation — and dispatch is ALREADY symbol-based (the model binds each call to its `Enumerable.*` method; the query-syntax lowering hands the same symbols through `MapSymbol`). The 42 are also the largest block of sites still lacking the writer's punctuation, which is the `f ?? g && g` class of bug | |
| 2 | **The numeric BCL as a table.** Method symbol to a JS expression template, one generated conformance case per entry | The 71 EQ1001 members are PURE FUNCTIONS with a 1:1 mapping (`Double.AcosPi(x)` is `Math.acos(x) / Math.PI`). No refactor, pure addition, and every entry is independent of every other — the one slice in this plan that parallelises cleanly | done — 71 → 6 fences (Int128 BigMul, the two Unicode-table members, the intern pool, the two PLATFORM estimates — .NET on ARM64 answers FRECPE, a number this side cannot produce). 65 entries in PrimitiveStaticStrategy's tables + `$eq.bits` (rotations, counts) + `$eq.math` (min/max tie rules, FMA by TwoProduct, bit-adjacent doubles, and the *Pi family PORTED LITERALLY from dotnet/runtime's aocl-libm polynomials — .NET never calls the platform libm for those, so the only bit-exact answer is running the same reduction into the same coefficients; probing found TanPi(0.25) is 0.9999999999999999 THERE too). 135 conformance cases, magnitude ties, ±0, parity infinities and residue-error paths included |
| 3 | **Widen the differential generator** to LINQ chains and statements | After 1 and 2 there is a large new surface, and the generator is the only instrument that finds what nobody thought to test. Its grammar is still expression-heavy | |
| 4 | **Component conformance.** Execute component BEHAVIOUR — lifecycle, events, reconciliation — on both sides | The hole in the net: everything built so far executes both sides at the LANGUAGE level. Component behaviour has pins and Studio walks but no executed twin, which is why "a stateful component returned as a build's ROOT lost its state" (b83ced2e) had to be found by hand | |

## When this phase is DONE

A plan without a stop condition chases an ideal forever. Three numbers end it:

1. `ir-migration.baseline.txt` reaches ~20 — only genuinely irregular constructs still emit text.
2. `bcl-surface.baseline.txt` fences only what is impossible BY CONSTRUCTION (`System.IO`,
   reflection, threading), not what is merely unwritten.
3. The differential generator runs N seeds clean across expressions, statements and LINQ.

When the three hold, the compiler is finished in the sense that matters, and the effort moves to
PERFORMANCE — which this whole arc has never once measured.

## Ordering note

(2) is cheap, low-risk and independent entry by entry; (1) touches working code and needs the net
tight before it starts. With two sessions on one repository they are the natural split, and they
do not overlap in a single file.
