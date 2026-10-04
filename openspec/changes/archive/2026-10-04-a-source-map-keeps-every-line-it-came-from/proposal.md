# Proposal

Closes #487, #490, #492 and #525, four Bugs under #164 (The transpiler's fences hold on every path).

## Why

Three carriers still wrote what they held as text before any writer saw it, so their statements, and
every line of a lambda's block they held, had no segment: the arrow a body with an `out` or `ref`
parameter runs in (#487), a member's expression body, and an object creation or initializer (#492),
59 lambda statements in the shared components. A default an interface supplies led its lines into
the implementing class's file, at lines it does not have (#490). And the map escaped five characters
by hand, so a control in a C# file made a map no JSON reader opens, as a tab in an app's name did to
the web manifest (#525).

## What Changes

- **The wrapper is IR.** A body with an `out` or `ref` parameter runs in an arrow whose block is the
  statement IR, built in one place for a method, a lambda and a local function, its own lines
  belonging to the declaration whose parameters they are. An iterator's buffer is IR as well. The
  arrow never awaits: C# refuses `ref` and `out` on an async method or lambda (CS1988).
- **A member's expression body takes the concise body's one lowering**, the one a lambda's and a local
  function's already take, converted at the depth of the block it stands in, and a body that returns
  nothing (a setter, a constructor) is a statement.
- **A creation and an initializer are IR.** `new T(…)` and an object literal are nodes of their own
  (`JsNew`, `JsObject`), so an object creation, an object or collection initializer and an anonymous
  object leave the text baseline. An anonymous member takes the name C# infers, which `new { a?.B }`
  had none of, and a collection initializer on a node adds one element per statement.
- **A map names every file.** Each mapping carries the tree it came from; the map numbers every file
  its mappings reach, names each by the project's layout and, in a full map, carries each one's text.
- **Every JSON the build lays out goes through one writer**, `eQuantic.UI.Codegen.JsonWriter`, whose
  escape is System.Text.Json's, and both maps (eqc's, and the one composed with the bundler's) are
  written by one map writer on top of it. The culture catalogs stay on System.Text.Json's serializer
  with its default, markup-safe encoder, since the server inlines them into a script.

For a developer using the SDK: in a Debug build, a breakpoint binds, and a frame thrown there names
its own line, inside a method with an `out` or `ref` parameter, inside a lambda held by an
expression-bodied member, an object creation, an initializer or an anonymous object, and inside a
default an interface supplies, which the debugger shows in the interface's own file. A C# file
holding a control character keeps its map, and an app name holding a tab keeps its web manifest.
The JavaScript does the same; a lowering's text changes in layout only.

## Capabilities

### New Capabilities

- `generated-files`: the files the build writes for a tool or a platform to read, a source map, a web
  manifest and an asset catalog among them, stay in their format whatever text they carry.

### Modified Capabilities

- `transpiler-source-maps`: a lambda's block maps statement by statement in the carriers above too;
  a body a lowering wraps maps statement by statement; a map names every file its segments come from.

## Impact

- eqc: `CodeGen/Ir` (`JsNew`, `JsObject`, `JsProperty`, `JsExprWriter`), `CSharpToJsConverter`,
  `MethodLowering`, `OutParameters`, `TypeScriptEmitter`, `RecordTypeEmitter`,
  `ObjectCreationStrategy`, `InitializerExpressionStrategy`, `AnonymousObjectCreationStrategy`,
  `LambdaExpressionStrategy`, `LocalFunctionStatementStrategy`, `QueueStackStrategy`; the source maps
  (`SourceMapGenerator`, `SourceMapComposer`, the new `SourceMapWriter` and `SourceMapSource`,
  `TypeScriptCodeBuilder.SourceMapping`, `ComponentCompiler`). eQuantic.UI.Codegen's `JsonWriter`.
  The runtime, the realizers, the Photon shells and the SDKs do not change; the runtime's source-map
  reader already reads a map of several files.
- Public surface: `SourceMapGenerator.Generate` takes the mappings and a function that describes
  each file (`SourceMapSource`) in place of one file's name and text; `SourceMapping.SourceFile`
  (a path) is `SourceMapping.Source` (the tree); `ConvertExpressionBodyIr` takes whether the body
  returns and answers a `JsBlock`; `JsExpr.New`, `JsExpr.Object`, `JsNew`, `JsObject`, `JsProperty`
  and `JsonWriter.Strings` are new; the three creation strategies are `IExpressionIrStrategy`
  implementations, `InitializerExpressionStrategy.ConvertInitializer` answering IR. Every retired
  signature is a `*REMOVED*` line in `PublicAPI.Unshipped.txt`. The developer surface does not move.
- The migration line: an app writes none of these, since they are eqc's own IR and map writer,
  which no app compiles against.
- Measured: the shared components' lambda statements with no line of their own go from 59 to 0, and
  the text strategies from 81 to 78.
