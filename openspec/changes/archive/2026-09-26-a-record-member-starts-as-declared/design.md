# Design

## How Dart does it

A Dart class declares its field initializers and its constructor's initializer list, and the
constructor runs them: a call site never repeats a default. The twin takes the same shape. Its
constructor is the one place a member's default is written, and a construction that skips a member
leaves it to the constructor, through JavaScript's own default parameters: an omitted argument and
`undefined` both run the default, so a named argument placed after a skipped one needs nothing
copied into the call.

## The default converts where its names resolve

An initializer is an expression like any other, so it converts through the same strategies, in the
twin's own module, where the names it reads (a static of the type, a sibling type) are imported. A
literal table in the call site could only hold what it could spell, which is why a decimal, a long,
a float or a `new()` came out wrong.

## Parameters in scope

A positional parameter is also a property, and a default reads the parameter: `Tag = "#" + Id` runs
in the constructor's parameter list, where `this.id` is not set yet, and a base clause runs before
`super()`, where reading `this` throws. A context flag, set only for those two conversions and put
back after, makes the parameter a bare name there, so a C# 12 class that captures a
primary-constructor parameter still reads it as state everywhere else.

## Not here

C# runs an initializer even when an object initializer then sets its member, and a `with` runs none
(#413); and C# zero-initializes every static before any static initializer runs (#417). Both are
measured and tracked; each changes every twin of its kind.
