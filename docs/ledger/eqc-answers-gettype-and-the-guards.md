- **2026-11-09 · eqc answers a comparison of runtime types and the argument guards as .NET does**: a task under
  #1127 ([#1202](https://github.com/eQuantic/equantic-ui/issues/1202)), found measuring the element engine for the
  runtime. `a.GetType() == b.GetType()`, Flutter's `canUpdate`, and every `ThrowIf` guard of the three argument
  exceptions failed the build with EQ2004. A comparison of runtime types now answers through `$eq.types` wherever
  the receiver's static type says the browser tells its runtime type apart (an exception, by the .NET type it
  carries, and a class or a struct whose twin is a class of its own, by its prototype and the type arguments it
  was built with, which a generic class is now marked with as a generic record is), and is refused where it
  cannot (a number, a string, an array, `object`, an interface). The twelve guards go through `$eq.guard`, with
  the parameter's name `CallerArgumentExpression` gives, read from the bound tree, the type's own equality or
  order, and .NET's words with the values in the current culture, measured on .NET 10: a NaN is negative, a
  negative zero is zero and negative, a null is `null` in an equality guard and nothing in an ordering one, and
  an ordering guard calls `CompareTo` through a null value. Held by two conformance classes executed on both
  sides, in the invariant culture and in pt-BR. Proposed and archived through OpenSpec (`transpiler-exceptions`,
  `transpiler-classes`).
