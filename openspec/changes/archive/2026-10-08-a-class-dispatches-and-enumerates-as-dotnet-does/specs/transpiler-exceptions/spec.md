## ADDED Requirements

### Requirement: An exception class of the app's is a class

An exception class an app declares that is not nested in another type SHALL be a class in the browser,
as any class of the app's is: its fields, its properties and their initializers, its constructors and
their bodies, and its methods SHALL be its own. Over an exception of .NET's, its base call, explicit or
implicit, SHALL hand what a `new` of that type hands: the message bound to the constructor's `message`,
or, where none or null was given, the text that constructor writes, and what it takes besides, each by
its parameter, every argument evaluated in the order it is written; `Message`, `InnerException` and
`ParamName` SHALL answer them as they answer for that type. A message still missing SHALL answer .NET's
default, `Exception of type '<its name>' was thrown.`, the type named as .NET names it at run time
(``App.Failed`1[System.Int32]``, `App.Outer+Inner`), and a member the class declares under the name of
one of the base's, an override of `Message` or of `ParamName` and a `Name` of its own, SHALL answer for
it. An exception of the class SHALL carry its .NET types, a derived class's its own and a constructed
generic class's those of its construction from before its constructor's body runs, which a typed
`catch`, a type pattern and an `as` read.

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

#### Scenario: A generic class that throws itself from its constructor

- **WHEN** `class Rethrown<T> : Exception` reads `Message` and throws `this` from its constructor, and
  `new Rethrown<int>(1, true)` is caught by `catch (Rethrown<string>) { … } catch (Rethrown<int> r) { … }`
- **THEN** the second clause takes it, and the message it read is
  "Exception of type 'App.Rethrown`1[System.Int32]' was thrown.", as in .NET, where neither clause took it
