- **2026-10-17 · A name the browser lacks fails the build, or answers as .NET does**: a method group
  of a .NET member went out as a name, so `Func<string, string> f = ts.ToString` was an unbound
  `ts.toString`, `items.Select(DateTime.Parse)` called the twin's parse with no text and handed
  `Select`'s index to its culture flag, `Select(Convert.ToDateTime)` named a `Convert` nothing defines
  and `Math.Round` as a delegate rounded a half up; the date, time and span strategies camel-cased
  every member of their types, so `ts.TryFormat(…)`, `ts.Multiply(…)`, `DateTime.Compare(…)` and 37
  other members named what no twin had, and `ts * 2` and `-ts` were NaN;
  `FormattableString.Invariant($"…")` and `string.Create(provider, $"…")` were refused as calls
  nothing translates; and a package's static, `Status.Label`, and a construction, `new Gauge("a")`,
  `new Random()` or `new Func<int>(…)`, named a class nothing defines. A method group of a member no
  twin carries is now the delegate its call is, bound where the group is written by a speculative
  model and translated as that call is, its receiver read once and a null one refused as .NET refuses
  it, and a group whose call has no translation fails the build with the call's own refusal; the date,
  time and span strategies claim only what their twins carry, and the twins gained the arithmetic
  members they lacked; an interpolation is written in the culture a call names; and a package's static
  or construction, and a construction of a .NET type no strategy builds, fail the build with EQ2004
  naming it. Thirty cases executed on both sides, 28 of them in the invariant culture, pt-BR and
  sv-SE, 37 refusal and build tests and the BCL audit's 10 moved verdicts pin them, and each case
  failed before ([#822](https://github.com/eQuantic/equantic-ui/issues/822),
  [#839](https://github.com/eQuantic/equantic-ui/issues/839)). Proposed and archived through OpenSpec
  (`openspec/specs/transpiler-expressions`, `transpiler-bcl`, `runtime-dates`, `referenced-projects`).
  Copilot's first review of #958 found nine more in the same family, each pinned on both sides and
  fixed: a span scaled by a long, a nullable number or a char and in place, a date's named
  arguments in another order, a FormattableString's holes formatted before the later ones ran, an
  object hole in the invariant culture, a delegate's capture hidden by its own parameter, one with
  an out built from another, and a package's collection built target-typed.
