# Spec Delta

## Purpose

How eqc names what it writes: a local function, a reserved word, and every declaration a reader
reads, so that no name it changes lands on one its scope already holds.

## ADDED Requirements

### Requirement: A renamed local function lands on no name its scope holds

A local function SHALL be camel-cased and made a legal JavaScript identifier, and SHALL take a `$`
where the member around it already declares that name. Its declaration, a method group and a direct
call SHALL all use the same name.

#### Scenario: A local and a local function that differ by case

- **WHEN** `string log = ""; var d = new Dictionary<string, int> { ["a"] = 1 }; int D() { log += "d"; return 7; } d.GetValueOrDefault("a", D()); return log;` runs
- **THEN** it answers `"d"`, as in .NET, and the module loads

#### Scenario: A parameter named like the function

- **WHEN** `int D() => 7; int Use(int d) => d + D(); return Use(1);` runs
- **THEN** it answers 8, as in .NET

### Requirement: A reserved word is escaped with a `$`

A name that is a JavaScript reserved word or literal SHALL be escaped with a `$`, which no C#
identifier can hold, on every kind of declaration that writes a name.

#### Scenario: A C# name that ends like the old escape

- **WHEN** `int package = 1; int package_ = 2; return package * 10 + package_;` runs
- **THEN** it answers 12, as in .NET

#### Scenario: A verbatim keyword

- **WHEN** `int @class = 3; return @class;` runs
- **THEN** it answers 3, as in .NET

### Requirement: A declaration and its readers use one spelling

Every parameter (of a component's constructor, a record's method or constructor, and a server
action's stub and the call it forwards), local, and pattern, deconstruction, loop or catch variable
SHALL be declared under the same legal name its readers use.

#### Scenario: A record method with a verbatim keyword parameter

- **WHEN** `public record Box(int N) { public int Plus(int @class) => N + @class; }` runs `new Box(3).Plus(4)`
- **THEN** it answers 7, as in .NET

### Requirement: A parameter the body never reads keeps its spelling free

The spelling of a parameter the body never reads (its name with an underscore in front) SHALL be
reserved, so that no local function takes it.

#### Scenario: An unused verbatim parameter beside an underscored function

- **WHEN** `public int M(int @class) { int _class() => 1; return _class(); }` is transpiled
- **THEN** the parameter is `_class$` and the function a different name, so the module declares each once
