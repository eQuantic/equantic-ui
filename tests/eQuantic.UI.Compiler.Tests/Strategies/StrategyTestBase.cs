using eQuantic.UI.Compiler.CodeGen;

namespace eQuantic.UI.Compiler.Tests.Strategies;

/// <summary>
/// The HEURISTIC-MODE harness, on purpose: <see cref="CSharpToJsConverter.Convert(string)"/>
/// parses a bare snippet with no semantic model, which is exactly the world a standalone host
/// (no project compilation handed over) lives in. Under the two-regime rule
/// (<c>ConversionContext.CanGuess</c>) name heuristics are legitimate there and nowhere else, so
/// these tests are the coverage OF that mode — not a stand-in for the authoritative one, which
/// <c>TestHelper</c> exercises with a real compilation. A behavior that must hold in both worlds
/// gets a test in each.
/// </summary>
public abstract class StrategyTestBase
{
    protected string Convert(string code)
    {
        var converter = new CSharpToJsConverter();
        return converter.Convert(code);
    }
}
