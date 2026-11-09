## Context

`GetType()` is fenced (`PlatformMemberRead.NothingCarries`) because the browser cannot answer it for every value: a C# `int`, `double` and `float` are one JavaScript number, a `string` and a `char` one JavaScript string, and every array, tuple and plain object shares one prototype. The guards were never translated: each reached the invocation fallback, which refuses a BCL method it does not know.

The element engine needs one shape of each. Flutter's `Widget.canUpdate` compares `runtimeType`, which Dart answers for every object because Dart reifies its generics: `Box<int>` and `Box<String>` are two runtime types. That is the bar for the comparison: the browser answers it where it knows the runtime type as well as Dart would, and refuses it where it does not.

## Goals / Non-Goals

**Goals:**

- Compare runtime types wherever the browser tells them apart, and refuse at build time where it does not.
- Every `ThrowIf` guard of the three argument exceptions, faithful to .NET's checks, words, culture, `ParamName` and `ActualValue`.

**Non-Goals:**

- `GetType()` as a value (`.Name`, `.FullName`, a `Type` held in a variable or a dictionary key): it stays refused. A `Type` the browser could answer for some values only is a bigger promise than the comparison.
- `ObjectDisposedException.ThrowIf`, whose message names the full .NET name of an instance's type: #1203.
- A negative zero `decimal`: the runtime's `Decimal` keeps its sign on the mantissa and has none for zero, so `ThrowIfNegative(-0.0m)` passes where .NET throws. A gap of the decimal's representation, not of the guard.

## Decisions

### A runtime type is a token the runtime builds from the value

`x.GetType()` lowers to `$eq.types.of(x)` and `typeof(X)` to `$eq.types.named(X, closure?)`, or to the exception's .NET name for an exception type; `==` lowers to `$eq.types.same(t, u)` and `!=` to its negation. `of` throws the NullReferenceException for a null receiver, and since each token is an argument of `same`, the first receiver is checked before the second is evaluated, as C# checks it.

A token is the value's prototype and its closure mark, or, for an exception, its most derived .NET type (`typesOf(e)[0]`, the name `ExceptionTypes.NameOf` writes, type arguments included). Two tokens are the same type when their prototypes are one and their marks do not disagree, which is `sameClosure`'s rule: a value with no mark is not taken for another type.

Alternative rejected: comparing `constructor`s. A twin's `constructor` can be shadowed by a member named `constructor`, and the prototype is what `instanceof` already reads for every type test.

### What qualifies is decided by the static type, in one place

A receiver qualifies when its static type (a `Nullable<T>` read as its `T`, a type parameter through its class constraints) is an exception, or a class or a struct whose twin is a class of its own: the rule `PatternConverter.TypeCheck` already applies before it writes `instanceof` (`LowersToAJsClass`, `IsEmittedAppType`), which becomes one internal predicate, `IsAJsClass`, that both read. The comparison also takes the vocabulary's roots themselves (`VisualNode`, `UiComponent`) as receivers, whose values are always instances of their derived classes; a type test of a root is left as it is. Every other static type is refused with EQ2004: its values may be held as a primitive or a plain object whose prototype is shared.

### A generic class is marked as a generic record is

`ObjectCreationStrategy.ClosureOf` marks a generic record or struct the source declares, built as a closed type, with its type arguments (#651). The mark now covers a generic class too: without it, every closed type of a generic class shares a prototype and the comparison would answer true for `Box<int>` against `Box<string>`. The mark is held in a `WeakMap` beside the value, so nothing else a class does reads it. The alternative, refusing a receiver whose type is or may hold a generic class, cannot be decided: a non-generic base is extended by generic classes the build does not see.

### A guard is one runtime function per .NET method

`ArgumentGuardStrategy` claims a call bound to one of the twelve methods (by symbol, so `using static` and an alias are claimed alike) and writes `$eq.guard.<name>` over the arguments in parameter order, through a template whose holes point at the written arguments, so a named argument out of order is evaluated where it is written. What the runtime cannot know is handed by the compiler:

- the parameter's name: the written argument, or, where the call leaves it out, the constant the bound tree's default argument carries, which Roslyn fills from `CallerArgumentExpression`;
- for `ThrowIfEqual`/`ThrowIfNotEqual`, the type's default equality (`ElementEquality.Comparer`), which already answers NaN equal to NaN and `-0.0` equal to `0.0`;
- for the ordering guards, the type's `Ordering` (`ValueOrdering.Of`), a `comparable` one called through the value's own `compareTo`, as .NET calls `value.CompareTo(other)`; a type with no faithful ordering is refused;
- the text of a value, as `string.Format` writes it in the current culture: an arrow over `StringConversion.Of(T, v)`, called only when the guard throws.

The runtime composes the message from .NET's resource strings, measured on .NET 10, and builds the exception through `exceptions.ts` with `paramName`, `actualValue` and the new `actualText`, which the message's last line writes in place of the value's invariant text.

A sign is read from the value itself: a number is negative when below zero, a negative zero or a NaN (.NET's `double.NaN` has its sign bit set, and the browser cannot see a NaN's sign), a long when below `0n`, a decimal by its comparison to zero, a char never.

## Risks / Trade-offs

- [A value rebuilt from JSON is a plain object] → It shares `Object.prototype` with every other plain object, as it fails every `instanceof` today; the known caveat of type tests, unchanged.
- [A generic class built inside generic code carries no mark] → It is not taken for another closed type, the rule records follow; a factory a source generator writes is such code, so two generic components built through their factories compare by their class alone.
- [A guard's text arrow is allocated per call] → It is a closure over nothing; a hot path pays one allocation, measured against the throw it replaces nowhere.
