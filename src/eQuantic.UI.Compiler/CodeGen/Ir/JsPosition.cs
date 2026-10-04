namespace eQuantic.UI.Compiler.CodeGen.Ir;

/// <summary>A line and a column in a writer's text, both counted from zero at its start.</summary>
internal readonly record struct JsPosition(int Line, int Column);
