using eQuantic.UI.Code;
using eQuantic.UI.Primitives;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// The engine behind a code surface a test found in a frame, and the grid it draws on. A realizer
/// sees only <see cref="ICodeSurfaceModel"/>; a test is allowed to look further, because what it
/// asserts is that the host and the engine agree.
/// </summary>
internal static class CodeSurfaceProbe
{
    public static CodeEditorController Engine(this CodeSurface surface) => (CodeEditorController)surface.Model;

    public static CodeGrid Grid(this CodeSurface surface) => surface.Engine().Grid;
}
