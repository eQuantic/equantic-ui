# Tasks

## 1. A method group of a member no twin carries (#839 a, d)

- [x] 1.1 Lower every group of a member of a type no twin carries in `MethodGroupStrategy`, asked before every strategy that claims a member access or a name: an arrow over the method's own parameters whose body is the call, bound where the group is written by a speculative model `SemanticHelper.Speculate` answers for
- [x] 1.2 Read the receiver once where the delegate is made, copied as C# boxes it, and refuse a null one as .NET does (`$eq.delegateTarget` for a method called directly, `$eq.receiver` for a virtual one)
- [x] 1.3 Report a refused call at the group, and refuse with EQ2004 a group whose method hands a value back through an out or a ref
- [x] 1.4 Drop the group paths of `CharMethodStrategy` and `CancellationStrategy`, now the rule's, and build a delegate written with `new` as the function it is built from
- [x] 1.5 Pin `Select(DateTime.Parse)`, `Select(TimeSpan.Parse)`, `Select(Convert.ToDateTime)`, `Math.Round`, a span's and a string's instance groups, a null receiver and a group through `using static` in `BclMethodGroupConformanceTests`, in pt-BR and sv-SE where a culture reads, executed on both sides

## 2. A date's, a time's, an instant's and a span's members (#839 b)

- [x] 2.1 Claim in `TimeSpanStrategy`, `DateTimeStrategy`, `DateOnlyTimeOnlyStrategy` and `DateTimeOffsetStrategy` only the members their twins carry, so the rest falls to EQ2004
- [x] 2.2 Give the twins and the factories the arithmetic members they lacked, a span's `*`, `/` and sign with .NET's rounding and refusals, and the span between two dates or two instants
- [x] 2.3 Pin them in `TimeSpanMemberConformanceTests` and `DateMemberConformanceTests`, and the refused ones in `UntranslatedNameTests`

## 3. An interpolation in the culture a call names (#839 c)

- [x] 3.1 Write `FormattableString.Invariant`, `FormattableString.CurrentCulture` and `string.Create(provider, …)` in `InterpolationInACultureStrategy`, the holes in the culture the call names through `ValueFlow` and `InterpolatedStringStrategy`
- [x] 3.2 Refuse another culture with EQ2108 and a `FormattableString` or an `IFormattable` made elsewhere with EQ2004
- [x] 3.3 Pin them in pt-BR and sv-SE in `InterpolationInACultureConformanceTests`

## 4. A package's type and a construction with no twin (#822)

- [x] 4.1 One predicate, `TwinlessTypeExtensions`, asked by the call fence, the read fence, the `using static` fence, the construction fence and the group rule
- [x] 4.2 Refuse a package's static read, its `using static` read and call, and its construction, with EQ2004 naming the member, the type and the assembly; refuse a construction of a .NET type no strategy builds
- [x] 4.3 Pin them, a target-typed construction and what keeps building in `APackagesTypeTests`, and the .NET constructions in `UntranslatedNameTests`

## 5. Proof and documentation

- [x] 5.1 Move the BCL audit's baselines where verdicts moved to `fenced:EQ2004`, and the null-argument gaps the group rule now refuses
- [x] 5.2 Run the compiler, conformance, Web and Server suites and the runtime's, and build the dashboard sample
- [x] 5.3 `docs/DIAGNOSTICS.md`'s EQ2004 row, and the wiki's Diagnostics and SupportedFeatures in the five languages, on the wiki branch of the same name
- [x] 5.4 One ledger entry under `docs/ledger/` citing the issues
