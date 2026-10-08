# Spec Delta

## ADDED Requirements

### Requirement: A null argument is answered as .NET answers it

A BCL member eqc translates SHALL answer a null argument as .NET answers it: where .NET refuses the
null, with an `ArgumentNullException` naming the parameter, the browser SHALL refuse it with that type,
that `ParamName` and .NET's message, and where .NET takes the null, the browser SHALL answer what .NET
answers. The conformance suite SHALL call every member of the translated surface, derived from the BCL
audit's committed record and never listed by hand, with a null for each of its reference parameters,
on both sides, and SHALL fail on a probe that answers otherwise and is not in its committed list of
gaps. That list SHALL only shrink, and each of its entries SHALL say why .NET's answer is out of reach
there.

#### Scenario: A LINQ selector that is null

- **WHEN** `var a = new List<int> { 1 }; Func<int, int> f = null; try { return a.Max(f).ToString(); } catch (ArgumentNullException e) { return e.ParamName; }` runs
- **THEN** it answers "selector", as .NET does, where the selector was called through null and the
  `NullReferenceException` went past the clause

#### Scenario: A sequence its operator names

- **WHEN** `var a = new List<int> { 1 }; HashSet<int> b = null; try { return a.Concat(b).Count().ToString(); } catch (ArgumentNullException e) { return e.ParamName; }` runs
- **THEN** it answers "second", as .NET does, where every sequence was refused as "source"

#### Scenario: A null compared

- **WHEN** `object o = null; bool b = false; return b.CompareTo(o);` runs
- **THEN** it answers 1, as .NET does, where it answered -1

#### Scenario: A constructor's own parameter

- **WHEN** `string text = null; try { return new Guid(text).ToString(); } catch (ArgumentNullException e) { return e.ParamName; }` runs
- **THEN** it answers "g", as .NET does, where it answered Parse's "input"

#### Scenario: A gap the list does not know

- **WHEN** a member of the translated surface answers a null otherwise than .NET, and its probe is not
  in `null-argument-gaps.baseline.txt`
- **THEN** `NullArgumentConformanceTests` fails, naming the probe, what each side did and the
  JavaScript eqc wrote

#### Scenario: A gap that closes

- **WHEN** a probe the list names answers as .NET does
- **THEN** the suite fails until the entry leaves the list, regenerated with
  `EQ_UPDATE_NULL_ARGUMENT_BASELINE=1`
