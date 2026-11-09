## ADDED Requirements

### Requirement: A runtime type compares as .NET compares it

`a.GetType() == b.GetType()`, `a.GetType() != b.GetType()`, and either one against `typeof(X)` in either
order, SHALL answer as .NET answers wherever the browser can tell the types apart: a value of a class or a
struct whose twin is a class of its own (the app's, the vocabulary's, the transpiled libraries'), and an
exception. The receivers SHALL be evaluated once, in the order they are written, and a null one SHALL
throw a NullReferenceException before the next is evaluated. A generic type's values SHALL be told apart
by their type arguments wherever the build marked them; a value built where its type arguments are known
only at run time carries no mark, and SHALL compare equal to every closed type of its own definition,
because the browser cannot tell which one it was built as. A receiver whose type does not let the browser tell
its runtime type apart (a number, a string, a bool, a char, an enum, an array, a tuple, a delegate,
`object`, an interface, or a type parameter with no class constraint that does) SHALL be refused with
EQ2004, and so SHALL a `typeof` of such a type and every other use of `GetType()`.

#### Scenario: Two values of one class and of two

- **WHEN** `class A : Node {}`, `class B : Node {}` and `Node x = new A(), y = new A(), z = new B();` are
  compared as `x.GetType() == y.GetType()` and `x.GetType() == z.GetType()`
- **THEN** they answer true and false, as in .NET, where the build failed with EQ2004

#### Scenario: A derived class against its base

- **WHEN** `class B : A {}` and `A a = new B();` is compared as `a.GetType() == typeof(A)` and
  `a.GetType() != typeof(B)`
- **THEN** both answer false, as in .NET

#### Scenario: Two closed types of one generic class

- **WHEN** `class BoxBase {}`, `class Box<T> : BoxBase {}` and `BoxBase x = new Box<int>(), y = new Box<string>(), z = new Box<int>();`
  are compared as `x.GetType() == y.GetType()` and `x.GetType() == z.GetType()`
- **THEN** they answer false and true, as in .NET

#### Scenario: Two exceptions

- **WHEN** `Exception e = new ArgumentException();` is compared as `e.GetType() == typeof(ArgumentException)`
  and `e.GetType() == typeof(Exception)`
- **THEN** they answer true and false, as in .NET

#### Scenario: A null receiver

- **WHEN** `Node x = null;` is compared as `x.GetType() == Make().GetType()`
- **THEN** it throws a NullReferenceException and `Make` is not called, as in .NET

#### Scenario: A receiver the browser cannot tell apart

- **WHEN** `object o = 1; o.GetType() == typeof(int)` is built
- **THEN** the build fails with EQ2004, naming the type the receiver is held as

### Requirement: A generic class built as a closed type keeps its type arguments

A generic class the app declares, built with `new` as the closed type C# names, SHALL be marked with its
type arguments as a generic record or struct is, so that what compares closed types (a runtime type's
comparison) tells `Box<int>` from `Box<string>`. Its equality, its text and its JSON SHALL NOT change.

#### Scenario: A mark that changes nothing else

- **WHEN** `class Box<T> { public T Value; }` and `new Box<int> { Value = 1 }` is built
- **THEN** its `Value`, its `Equals`, its `ToString` and its JSON answer as before, and its runtime type is
  not `Box<string>`'s
