using Xunit;
using eQuantic.UI.Compiler.CodeGen;
using eQuantic.UI.Compiler.Models;

namespace eQuantic.UI.Compiler.Tests;

public class SnapshotTests
{
    [Fact]
    public void Emit_SimpleComponent_MatchesSnapshot()
    {
        // Assemble
        var component = new ComponentDefinition
        {
            Name = "TestComponent",
            BaseClassName = "StatelessComponent",
            IsStateful = false,
            BuildMethodNode = null, 
            BuildTree = new ComponentTree 
            {
                ComponentType = "div",
                Properties = new Dictionary<string, PropertyValue>
                {
                    { "ClassName", new PropertyValue { Type = PropertyValueType.String, StringValue = "test-class" } }
                }
            }
        };

        var emitter = new TypeScriptEmitter();
        
        // Act
        var result = emitter.Emit(component);
        
        // Assert
        var expected = @"
import { BuildContext, Component, HtmlElement, StatelessComponent } from ""@equantic/runtime"";
import { div } from ""./div"";

export class TestComponent extends StatelessComponent {
    build(context: BuildContext) {
        return (
        new div({

            className: 'test-class',
        }
        )
        );
    }

}";
        
        // Normalize line endings and whitespace for comparison
        var normalizedResult = Normalize(result);
        var normalizedExpected = Normalize(expected);
        
        Assert.Equal(normalizedExpected, normalizedResult);
    }
    
    private string Normalize(string input)
    {
        return input.Replace("\r\n", "\n").Replace("\t", "    ").Trim();
    }
}
