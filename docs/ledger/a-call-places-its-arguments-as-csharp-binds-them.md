- **2026-11-02 · A call evaluates its arguments as written, and an out argument shifts none**: a bug under #849
  ([#1071](https://github.com/eQuantic/equantic-ui/issues/1071)). Three paths placed a call's arguments from its
  syntax, an invocation, a vocabulary type's constructor with a twin of its own and a twin's constructor where no
  bound tree answered, and passed named arguments in parameter order, so their effects ran in the signature's order
  in the browser; and an invocation, which converts no `out` argument, paired the argument after one with the
  value of the next. Each written argument is now placed by its index in the list written, and the arguments are
  evaluated in the order written through temporaries ahead of the first one passed. Proved both sides executed in
  `WrittenArgumentOrderConformanceTests`. Proposed and archived through OpenSpec (`transpiler-expressions`).
