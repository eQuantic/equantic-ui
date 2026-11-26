# Proposal

Closes #1865, a Bug under #1587 (The transpiler's fences hold on every path, part five), found by the secondary review
of #1864.

## Why

Since #1858 an `out var` of a named type is declared by its type in TypeScript, but only by the statements that put it
in the enclosing block. Every other scope that declares one still wrote `: any`: a loop's condition, a `using`, a
`catch` filter, a switch's sections and guards, a switch expression's arms, an initializer and a constructor's base
call. A list read through such a variable and walked by a `foreach` inferred `unknown` elements, which the runtime's
`tsc` refuses at the first use (TS18046), the error #1858 removed for an `if`.

## What Changes

- `ExpressionVariableScanner` types names gathered from several expressions, each `out var` from the expression that
  declares it, and writes a loop head's list with the same types. A definite assignment is no part of a `for` head's
  grammar, so a typed name there starts as `undefined!`, which strips to `let next = undefined`, as an uninitialized
  `let` starts.
- Every scope asks it: `while`, `do` and `for` with their heads, `using` with its resource, `try` with its filters, a
  switch with its sections' statements and its guards, a switch expression with its arms, and an initializer and a
  constructor's base call through the converter, whose annotations are their own.
- A method's own `out` parameter, which its body assigns inside the arrow the callee contract writes, is declared by
  its type the same way (`OutParameters.Body`, through `CSharpToJsConverter.OutParameterType`). Four library modules
  move, their annotations only: `CodeDocument`, `CodeHistory`, `CodeSnippetExpansion` and `SheetReference`.
- A `using` with no declaration holds its resource under one fixed name, `$disposable`, in the block its lowering
  opens. It drew the name from a fresh `Guid`, so the same C# wrote another module on every compile, which the shape
  holding such a `using` met as a pin that never held.
- The overloads that took an expression and a `bool` are gone, since no caller needs one. The ones that take names and
  a `bool` stay for what no `out var` types: a pattern's held parts and a lowering's temporaries.

No module's JavaScript changes, and nothing an app writes moves. The compiler is a tool surface, whose removals are
free.

## Impact

- `src/eQuantic.UI.Compiler`: `ExpressionVariableScanner`, the loop, `using`, `try`, switch and switch-expression
  strategies, `CSharpToJsConverter.ConvertInitializer`, `TwinConstructor`
- The emission shapes the runtime's `tsc` reads: `EmittedScopes`, `EmittedScopedBase`, `EmittedLease`
- `openspec/specs/transpiler-expression-variables`: the requirement that an expression variable is declared by its
  statement, with C#'s scope
- `openspec/specs/transpiler-statements`: a new requirement, that a using with no declaration writes the same module
  on every compile
- An entry under `docs/ledger/`
