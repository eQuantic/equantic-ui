- **2026-11-12 · A for head's variable keeps the type C# declared**: a Bug under #1174
  ([#1483](https://github.com/eQuantic/equantic-ui/issues/1483)). `for (Element? at = this; …; at = at.Parent)`
  was written `let at = this`, which TypeScript types as the polymorphic `this` and then refuses the walk up to
  the parent. The head's declarators are written as a local declaration writes them, and a declaration C# made
  nullable, or one initialized with `this`, says its type though the initializer has the same one.
