# transpiler-exceptions Specification

## Purpose
How eqc lets C# throw and catch in the browser as .NET does: an exception carries its .NET types,
a catch clause takes the exception its type names and its filter accepts, in C#'s order, and a
rethrow rethrows the exception caught.

## Requirements

### Requirement: An exception carries its .NET type

A .NET exception in the browser SHALL carry the .NET types it is, the most derived first and
`System.Exception` last. `new T(…)` for an exception type, decided by T's symbol whatever its name and
including an app's own class and the target-typed `new(…)`, SHALL build one with T's chain, and an
exception the runtime throws on .NET's behalf SHALL be of the type .NET throws for the same operation.
A JavaScript `TypeError` SHALL read as a `NullReferenceException`, and any other error without a chain
as an `Exception` and nothing more specific. A type pattern, a switch arm and an `as` over an exception
type SHALL test that type.

#### Scenario: A type pattern over exceptions

- **WHEN** `Exception e = new ArgumentNullException("p"); return $"{e is ArgumentException}{e is InvalidOperationException}{e is Exception}";` runs
- **THEN** it answers `TrueFalseTrue`, as .NET does

#### Scenario: An overflow the runtime throws

- **WHEN** `int m = int.MaxValue; try { return checked(m + 1); } catch (DivideByZeroException) { return 1; } catch (ArithmeticException) { return 2; }` runs
- **THEN** it answers 2, as .NET does

#### Scenario: A member read through null

- **WHEN** `string s = null; try { return s.Length; } catch (ArgumentException) { return 1; } catch (NullReferenceException) { return 2; }` runs
- **THEN** it answers 2, as .NET does

#### Scenario: A null argument a .NET method refuses by name

- **WHEN** `DateTime.Parse(null!)`, `string.Format(format, 1)` with a null `format`, or
  `new string((char[])null!, 0, 0)` runs inside `try` with clauses for `NullReferenceException` and
  `ArgumentNullException`
- **THEN** the `ArgumentNullException` clause takes it, its message naming the parameter as .NET's
  does, where reading the argument through null threw a NullReferenceException

#### Scenario: An exception class of the app's own

- **WHEN** `class GateClosedException : InvalidOperationException` is thrown and caught by
  `catch (ArgumentException) { … } catch (GateClosedException e) { return e.Message; }`
- **THEN** the second clause takes it and answers its message, as .NET does

### Requirement: A catch clause takes the exception its type names

The catch clauses of a `try` SHALL be tried in order, as C# tries them: the first whose type takes the
exception, its own type or one derived from it, and whose filter, if any, answers true, SHALL run, and
an exception no clause takes SHALL be thrown on. A clause with no type, or of `System.Exception`, SHALL
take every exception.

#### Scenario: Two clauses

- **WHEN** `try { throw new ArgumentException("x"); } catch (InvalidOperationException) { return 1; } catch (ArgumentException) { return 2; }` runs
- **THEN** it answers 2, as .NET does, where the module did not parse

#### Scenario: A type that does not match

- **WHEN** `try { try { throw new ArgumentException("x"); } catch (InvalidOperationException) { return 1; } } catch (Exception) { return 2; }` runs
- **THEN** it answers 2, as .NET does, where the inner clause took the exception and answered 1

### Requirement: A filter decides whether its clause takes the exception

A clause's `when` filter SHALL run when the clause's type takes the exception, with the clause's
variable bound, and SHALL decide whether the clause takes it. A filter that throws SHALL answer false,
and the exception it was asked about SHALL go on to the next clause. The variables a filter declares
SHALL be declared, and its clause's block SHALL read them.

#### Scenario: A filter that refuses

- **WHEN** `try { try { throw new Exception("a"); } catch (Exception e) when (e.Message == "b") { return 1; } } catch (Exception) { return 2; }` runs
- **THEN** it answers 2, as .NET does, where the filter was dropped and the inner clause answered 1

#### Scenario: A filter that declares a variable

- **WHEN** `try { throw new FormatException("12"); } catch (Exception e) when (int.TryParse(e.Message, out var n)) { return n; }` runs
- **THEN** it answers 12, as .NET does

#### Scenario: A filter that throws

- **WHEN** `Exception x = null; try { try { throw new InvalidOperationException("a"); } catch (Exception e) when (x.Message == "z") { return "first"; } } catch (InvalidOperationException e) { return "outer:" + e.Message; }` runs
- **THEN** it answers `outer:a`, as .NET does

### Requirement: A rethrow rethrows the exception caught

`throw;` SHALL rethrow the exception its clause caught, whatever the clause's variable has been
assigned since.

#### Scenario: A rethrow after the variable is reassigned

- **WHEN** `try { try { throw new Exception("a"); } catch (Exception e) { e = new Exception("b"); throw; } } catch (Exception e) { return e.Message; }` runs
- **THEN** it answers `a`, as .NET does, where `throw;` was a SyntaxError

### Requirement: An exception's object initializer is applied once it is built

`new T(…) { … }` for an exception type SHALL build the exception with T's chain, as any construction
of T does, and SHALL then apply the object initializer to it as a record's is applied: each member
it names is assigned, in the order it is written, after the exception is built.

#### Scenario: A field set by an initializer

- **WHEN** `class Failure : Exception { }`, `class Retry : Failure { public int Attempts; }` and
  `new Retry { Attempts = 3 }`
- **THEN** its `Attempts` is `3`, and it is a `Failure`, as in .NET

#### Scenario: Two members, each evaluated after the exception is built

- **WHEN** `class Coded : Exception { public int Code; public string Hint { get; set; } }` and
  `new Coded { Code = 4, Hint = "retry" }`
- **THEN** its `Code` is `4` and its `Hint` is `retry`, as in .NET

### Requirement: A framework exception's message is composed as .NET composes it

A framework exception built in C# SHALL take its message from the argument its constructor binds to
`message`, or, where none or null was given, the text .NET writes for that constructor, read from
.NET itself, and SHALL end it as .NET ends it: the parameter's name as ` (Parameter 'x')`, then the
actual value and a disposed object's name on lines of their own. Its `ParamName`, `ActualValue`,
`InnerException`, `ObjectName` and a type initializer's `TypeName` SHALL read what the constructor was
handed, and an aggregate's `InnerException`, the runtime's own included, SHALL be its first inner one.

#### Scenario: A parameter's name alone

- **WHEN** `new ArgumentNullException("x").Message` is read
- **THEN** it is "Value cannot be null. (Parameter 'x')", as in .NET, where it was "x"

#### Scenario: A message and a parameter's name

- **WHEN** `new ArgumentException("bad", "x").Message` is read
- **THEN** it is "bad (Parameter 'x')", as in .NET

#### Scenario: No message

- **WHEN** `new InvalidOperationException().Message` is read
- **THEN** it is "Operation is not valid due to the current state of the object.", as in .NET

#### Scenario: A type's own text, where its base has another

- **WHEN** `new TaskCanceledException().Message` is read
- **THEN** it is "A task was canceled.", as in .NET, where it was its base's "The operation was
  canceled."

#### Scenario: Two constructors of one type

- **WHEN** `new SystemException().Message` and `new SystemException(null).Message` are read
- **THEN** they are "System error." and "Exception of type 'System.SystemException' was thrown.", as
  in .NET

#### Scenario: A type initializer's null name

- **WHEN** `new TypeInitializationException(null, null).Message` is read
- **THEN** it is "The type initializer for '' threw an exception.", as in .NET, and `TypeName` reads
  the name a type initializer was given

#### Scenario: Eleven inner exceptions

- **WHEN** `new AggregateException(e0, …, e10).Message` is read
- **THEN** it ends with the eleven inner messages, as in .NET, where the eleventh argument was left
  in the generated code as text

#### Scenario: The aggregate a cancellation throws

- **WHEN** a cancellation's callbacks throw and `InnerException` is read from the aggregate it throws
- **THEN** it is the first exception gathered, as in .NET, where it was undefined

#### Scenario: The parameter's name as a member

- **WHEN** `ParamName` is read from `new ArgumentException("bad", "x")`
- **THEN** it is "x", as in .NET

### Requirement: An exception class of the app's is a class

An exception class an app declares that is not nested in another type SHALL be a class in the browser,
as any class of the app's is: its fields, its properties and their initializers, its constructors and
their bodies, and its methods SHALL be its own. Over an exception of .NET's, its base call, explicit or
implicit, SHALL hand what a `new` of that type hands: the message bound to the constructor's `message`,
or, where none or null was given, the text that constructor writes, and what it takes besides, each by
its parameter, every argument evaluated in the order it is written; `Message`, `InnerException` and
`ParamName` SHALL answer them as they answer for that type. A message still missing SHALL answer .NET's
default, `Exception of type '<its full name>' was thrown.`, and a member the class declares under the
name of one of the base's, an override of `Message` or of `ParamName` and a `Name` of its own, SHALL
answer for it. An exception of the class SHALL carry its .NET types, a derived class's its own and a
constructed generic class's those of its construction, which a typed `catch`, a type pattern and an
`as` read.

#### Scenario: Its members

- **WHEN** `class Failure : Exception { public int Code = 7; public List<int> Ids { get; } = new();
  public Failure() : base("failed") { Ids.Add(1); } public string Describe() => "f" + Code + ":" + Ids.Count; }`
- **THEN** `new Failure().Code` is `7`, `new Failure().Describe()` is `f7:1` and
  `new Failure().Message` is `failed`, as in .NET, where they were null, a throw and the empty string

#### Scenario: An inner exception

- **WHEN** `public Failure(string message, Exception inner) : base(message, inner) { }` and
  `new Failure("outer", new InvalidOperationException("inner"))`
- **THEN** its `Message` is `outer` and its `InnerException` is the `InvalidOperationException`, as in .NET

#### Scenario: A typed catch of a derived class

- **WHEN** `class Retry : Failure { public Retry() { Code = 9; } }` is thrown and caught by
  `catch (ArgumentException) { … } catch (Failure f) { return f.Describe() + "|" + (f is Retry); }`
- **THEN** it answers `f9:1|True`, as .NET does

#### Scenario: A constructed generic exception class

- **WHEN** `class Failed<T> : Exception` is thrown as `new Failed<int>(3)` and caught by
  `catch (Failed<string>) { … } catch (Failed<int> f) { return f.Value; }`
- **THEN** the second clause takes it and answers `3`, as .NET does

#### Scenario: The text of the .NET base it calls

- **WHEN** `class Closed : InvalidOperationException { }` and `new Closed().Message`
- **THEN** it is "Operation is not valid due to the current state of the object.", as in .NET

#### Scenario: A parameter's name its .NET base takes

- **WHEN** `class Bad : ArgumentException { public Bad(string name) : base("bad", name) { } }` and
  `new Bad("x")`
- **THEN** its `Message` is "bad (Parameter 'x')" and its `ParamName` is "x", as in .NET

#### Scenario: A member of the class's own under a name the base holds

- **WHEN** `class Lookup : Exception { … public string Name => key; }` and
  `class Renamed : ArgumentException { public Renamed() : base("m", "p") { } public override string ParamName => "q"; }`
- **THEN** `new Lookup("k").Name` is `k`, and `new Renamed()`'s `ParamName` is `q` and its `Message`
  "m (Parameter 'p')", as in .NET, where they read `Lookup` and `p`
