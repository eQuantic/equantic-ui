# Proposal

Closes #1756, a Bug under #1587.

## Why

A vocabulary record built with a named argument and an object initializer lost the initializer in the
browser. `new TextRun(code, Mono: true) { StyleOverride = … }` was written
`new TextRun(code, null, true, false, { … })`. The twin is `constructor(content, color, mono, config)`, so
the `false` landed in `config` and the initializer in a fifth slot nothing reads. The record's run drew
in the sentence's size once the page hydrated: since 0.2.0-preview.69 the browser builds a `Text`'s runs
itself.

The ordered arguments already place a named argument in the parameter the bound tree binds it to. The
vocabulary's path then filled the remaining defaults counting the arguments the SYNTAX wrote, two here,
where three parameters were already filled. The component's path had been cured of the same count
(`new Positioned(x, bottom: 0, start: 0)`), and the target-typed path counted what it emitted. One
construction path out of three still counted the syntax.

## What Changes

- Every construction that hands an initializer to the twin as its trailing config fills the remaining
  parameters' defaults from the arguments it EMITTED, through one helper the three paths share.

## Impact

- `src/eQuantic.UI.Compiler/CodeGen/Strategies/Expressions/ObjectCreationStrategy.cs`. No API changes. The
  runtime's transpiled components are unchanged byte for byte, since none of them writes this form.
