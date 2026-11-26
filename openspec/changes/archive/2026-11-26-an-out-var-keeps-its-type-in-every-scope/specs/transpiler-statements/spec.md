## ADDED Requirements

### Requirement: A using with no declaration writes the same module on every compile

A `using` whose resource is an expression and no declaration, `using (Open())`, SHALL hold the resource under one
fixed name the C# source cannot spell, in the block its lowering opens, so the same C# SHALL write the same module on
every compile, and a nested one SHALL hold and dispose its own.

#### Scenario: Two compiles of one using

- **WHEN** the same `using ((IDisposable?)null) { … }` is compiled twice
- **THEN** both write the same module, holding the resource as `const $disposable`; a name drawn from a fresh `Guid`
  wrote another one each time

#### Scenario: Two nested resources with no name

- **WHEN** `using (new Res(log, "a")) using (new Res(log, "b")) { log.Add("body"); }` runs
- **THEN** the log reads `body,b,a`, as in .NET
