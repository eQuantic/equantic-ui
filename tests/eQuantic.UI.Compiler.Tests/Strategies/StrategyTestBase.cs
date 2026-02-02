using eQuantic.UI.Compiler.CodeGen;

namespace eQuantic.UI.Compiler.Tests.Strategies;

public abstract class StrategyTestBase
{
    protected string Convert(string code)
    {
        var converter = new CSharpToJsConverter();
        return converter.Convert(code);
    }
}
