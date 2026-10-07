# Spec Delta

## ADDED Requirements

### Requirement: A framework exception's message is composed as .NET composes it

A framework exception built in C# SHALL take its message from the argument its constructor binds to
`message`, or the type's own text where none or null was given, and SHALL end it as .NET ends it:
the parameter's name as ` (Parameter 'x')`, then the actual value and a disposed object's name on lines
of their own. Its `ParamName`, `ActualValue`, `InnerException` and `ObjectName` SHALL read what the
constructor was handed.

#### Scenario: A parameter's name alone

- **WHEN** `new ArgumentNullException("x").Message` is read
- **THEN** it is "Value cannot be null. (Parameter 'x')", as in .NET, where it was "x"

#### Scenario: A message and a parameter's name

- **WHEN** `new ArgumentException("bad", "x").Message` is read
- **THEN** it is "bad (Parameter 'x')", as in .NET

#### Scenario: No message

- **WHEN** `new InvalidOperationException().Message` is read
- **THEN** it is "Operation is not valid due to the current state of the object.", as in .NET

#### Scenario: The parameter's name as a member

- **WHEN** `ParamName` is read from `new ArgumentException("bad", "x")`
- **THEN** it is "x", as in .NET
