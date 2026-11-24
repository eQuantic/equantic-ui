## ADDED Requirements

### Requirement: An HtmlElement subclass runs its constructor as C# does

A class deriving from `HtmlElement` SHALL run its constructor's body in the browser, a block's or an arrow's. Its
parameters SHALL keep their C# defaults, a primary constructor's parameters SHALL be held on each instance, and an
initializer beside the arguments SHALL be applied after the constructor, so every member it reads is what .NET reads.

#### Scenario: An explicit constructor and its default

- **WHEN** `Chip(string label, int size = 2) { _label = label; _size = size; }` is built as `new Chip("a", 5)` and
  `new Chip("b")`, and `Says()` returns `_label + "|" + _size`
- **THEN** they read `a|5` and `b|2`, as in .NET

#### Scenario: A primary constructor

- **WHEN** `class Tag(string label, int size = 2) : HtmlElement` is built as `new Tag("e", 9)`, and `Says()` returns
  `label + "|" + size`
- **THEN** it reads `e|9`, as in .NET

#### Scenario: An arrow constructor feeds what Render builds

- **WHEN** `Blocky(string text) => _text = text;` is built as `new Blocky("g")`, and `Render()` returns
  `new HtmlNode { Tag = "p", TextContent = _text }`
- **THEN** the node's tag is `p` and its text is `g`, as in .NET
