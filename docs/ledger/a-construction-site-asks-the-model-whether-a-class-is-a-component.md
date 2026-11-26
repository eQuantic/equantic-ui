- **2026-11-26 · One rule says whether a class is a component, asked of the model**: eqc answered it in three places,
  and only the parser's asked the semantic model (`IsUiComponent`). A construction site, deciding which arguments a
  component's constructor leaves out for its dependencies, and the field-naming rule each walked the bases for the
  names `StatelessComponent`, `StatefulComponent` and `UiComponent` in any namespace, where CLAUDE.md allows a name
  heuristic only where the model cannot be asked. Both ask `IsUiComponent` now and their walks are gone. No module
  moves: the pins, the constructor-injection tests and a regeneration that writes no file prove it
  ([#1839](https://github.com/eQuantic/equantic-ui/issues/1839)).
