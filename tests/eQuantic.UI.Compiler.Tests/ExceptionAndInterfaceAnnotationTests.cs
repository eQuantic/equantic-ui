using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Compiler.Tests;

/// <summary>
/// An annotation names a type the module can resolve. An exception is the JavaScript Error that
/// carries its .NET types (<c>utils/exceptions.ts</c>), and an interface crosses as <c>any</c>, as it
/// does in every other annotation: the code engine's completion was the first twin with a list of
/// providers, an event of exceptions and a list of them, and named <c>ICodeCompletionProvider</c> and
/// <c>Exception</c>, which no module defines (#296).
/// </summary>
public class ExceptionAndInterfaceAnnotationTests
{
    private static string Emit(string members) => TestHelper.ConvertClass(members);

    [Fact]
    public void AnExceptionInsideADelegate_IsAnError()
    {
        var js = Emit("public event Action<Exception>? Failed;");

        js.Should().Contain("(exception: Error) => void");
        js.Should().NotContain(": Exception");
    }

    [Fact]
    public void AnExceptionParameter_IsAnError()
    {
        var js = Emit("public string Describe(InvalidOperationException error) => error.Message;");

        js.Should().Contain("error: Error");
    }

    [Fact]
    public void AnEmptyListOfExceptions_OrOfAnInterface_NamesWhatTheModuleHas()
    {
        var js = Emit("""
                public int Count()
                {
                    var errors = new List<Exception>();
                    var owners = new List<IDisposable>();
                    return errors.Count + owners.Count;
                }
            """);

        js.Should().Contain("let errors: Error[] = []");
        js.Should().Contain("let owners: any[] = []");
    }
}
