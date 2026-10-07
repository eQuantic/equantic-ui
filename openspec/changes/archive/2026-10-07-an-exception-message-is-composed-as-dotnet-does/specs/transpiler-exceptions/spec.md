# Spec Delta

## ADDED Requirements

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
