- **2026-10-18 · A nullable struct answers as .NET does**: a member a struct declares, read through `?.`
  on a nullable value of it, was translated by its name, so `a?.Value` answered the struct itself,
  `a?.HasValue` whether it was null and `c?.Count` a `length` no struct has
  ([#966](https://github.com/eQuantic/equantic-ui/issues/966)); `NullableStrategy` now answers only the
  members `Nullable<T>` declares, by the symbol the model binds, and a count is read on the value's type.
  An empty nullable unwrapped to a value carried its null on, so `(Cents)n` over a struct's own
  `explicit operator Cents(int)` built a `Cents` holding null, and a cast, a `foreach` element and `Value`
  answered null ([#967](https://github.com/eQuantic/equantic-ui/issues/967)); every unwrap the bound tree
  applies now goes through `$eq.nullable.value`, which throws .NET's `InvalidOperationException`.
  `Equals` over a nullable struct, `Nullable.Equals` and `EqualityComparer<T>.Default.Equals` failed the
  build with EQ2004 ([#968](https://github.com/eQuantic/equantic-ui/issues/968)) and lower now to the
  comparison the type's default comparer is, two empty values equal and an empty one unequal to any; a
  struct's or a class's own `Equals` that is the app's code is asked every time, a value compared with
  itself included, behind a base that overrides nothing too, through `$eq.equalsOwn`, and
  `$eq.equalsOwnGeneric` in generic code, where an identity check had answered first (`v.Equals(v)` and
  `v.Equals(null)` over a struct value since #947), a record's synthesized `Equals` keeps the reference
  check C# writes into it, and only `EqualityComparer<T>`'s own `Default` is taken for the comparer. A
  comparison that reaches an `Equals` marked `[ServerOnly]`, which no twin holds, fails the build with
  EQ2010 at the call, as a call bound to it does, where it fell back to identity or to the members a
  struct's twin compares.
  The spreadsheet's fill move compares its preview as C# reads it, `FillTarget != before`, with no answer
  changed ([#969](https://github.com/eQuantic/equantic-ui/issues/969)). Ninety-five cases, and fifteen
  through an app's module graph, executed on both sides, pin it; 73 of the first and 14 of the second
  failed before. The `ToString` group of a nullable value, which wrote JavaScript's text when it was
  reported, was measured answering as .NET's already, and six more cases pin it
  ([#955](https://github.com/eQuantic/equantic-ui/issues/955)). Proposed and archived through OpenSpec
  (`openspec/specs/transpiler-expressions`, `openspec/specs/transpiler-bcl`,
  `openspec/specs/client-boundary`).
