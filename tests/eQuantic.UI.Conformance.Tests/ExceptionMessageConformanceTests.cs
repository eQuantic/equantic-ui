using eQuantic.UI.Conformance.Tests.Infrastructure;
using Xunit;

namespace eQuantic.UI.Conformance.Tests;

/// <summary>
/// A framework exception's message is composed as .NET composes it (#558): its own text where no
/// message, or a null one, was given, then the parameter's name, the actual value and a disposed
/// object's name. The constructor's arguments were read by position: the one argument of
/// <c>new ArgumentNullException(nameof(x))</c> was taken for the message, so a page showing
/// <c>e.Message</c> showed the parameter's name, and the parameter name never reached a message.
/// </summary>
public class ExceptionMessageConformanceTests
{
    private static string Thrown(string creation) =>
        $"try {{ throw {creation}; }} catch (Exception e) {{ return e.Message; }}";

    [SkippableTheory]
    [InlineData("new ArgumentNullException(\"x\")")]                              // "Value cannot be null. (Parameter 'x')"
    [InlineData("new ArgumentNullException(\"x\", \"gone\")")]                    // "gone (Parameter 'x')"
    [InlineData("new ArgumentNullException()")]                                   // "Value cannot be null."
    [InlineData("new ArgumentException(\"bad\", \"x\")")]                        // "bad (Parameter 'x')"
    [InlineData("new ArgumentException(\"bad\")")]                                // "bad"
    [InlineData("new ArgumentException()")]                                       // "Value does not fall within the expected range."
    [InlineData("new ArgumentException(null, \"x\")")]                            // "Value does not fall within the expected range. (Parameter 'x')"
    [InlineData("new ArgumentOutOfRangeException(\"x\")")]                        // "Specified argument was out of the range of valid values. (Parameter 'x')"
    [InlineData("new ArgumentOutOfRangeException(\"x\", \"too big\")")]           // "too big (Parameter 'x')"
    [InlineData("new ArgumentOutOfRangeException(message: \"m\", paramName: \"p\")")] // "m (Parameter 'p')": by its parameters
    [InlineData("new InvalidOperationException()")]                               // "Operation is not valid due to the current state of the object."
    [InlineData("new InvalidOperationException(\"io\", new FormatException())")]  // "io"
    [InlineData("new NotSupportedException()")]                                   // "Specified method is not supported."
    [InlineData("new NotImplementedException()")]                                 // "The method or operation is not implemented."
    [InlineData("new FormatException()")]                                         // "One of the identified items was in an invalid format."
    [InlineData("new KeyNotFoundException()")]                                    // "The given key was not present in the dictionary."
    [InlineData("new Exception()")]                                               // "Exception of type 'System.Exception' was thrown."
    [InlineData("new Exception(\"plain\")")]                                      // "plain"
    // A message .NET builds from the other arguments.
    [InlineData("new AggregateException(new FormatException(\"f\"))")]            // "One or more errors occurred. (f)"
    [InlineData("new AggregateException(new FormatException(\"a\"), new InvalidOperationException(\"b\"))")] // "One or more errors occurred. (a) (b)"
    [InlineData("new AggregateException(\"msg\", new[] { new FormatException(\"a\") })")] // "msg (a)": the array as it is
    [InlineData("new TypeInitializationException(\"App.T\", null)")]           // "The type initializer for 'App.T' threw an exception."
    public void AMessage_IsComposedAsDotNet(string creation)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet(Thrown(creation));
    }

    /// <summary>The actual value and a disposed object's name go on a line of their own, which .NET
    /// writes with the host's newline and the runtime with the SDK's.</summary>
    [SkippableTheory]
    [InlineData("new ArgumentOutOfRangeException(\"x\", 5, \"too big\")")]        // "too big (Parameter 'x')\nActual value was 5."
    [InlineData("new ArgumentOutOfRangeException(\"x\", true, \"m\")")]           // "m (Parameter 'x')\nActual value was True."
    [InlineData("new ObjectDisposedException(\"thing\")")]                        // "Cannot access a disposed object.\nObject name: 'thing'."
    public void AMessageOnTwoLines_IsComposedAsDotNet(string creation)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNetExceptTheHostsNewline(Thrown(creation),
            ".NET ends the message's first line with Environment.NewLine");
    }

    /// <summary>The members read what the constructor was handed.</summary>
    [SkippableTheory]
    [InlineData("try { throw new ArgumentException(\"bad\", \"x\"); } catch (ArgumentException e) { return e.ParamName; }")]                  // "x"
    [InlineData("try { throw new ArgumentNullException(\"y\"); } catch (ArgumentException e) { return e.ParamName; }")]                       // "y"
    [InlineData("try { throw new ArgumentOutOfRangeException(\"x\", 5, \"m\"); } catch (ArgumentOutOfRangeException e) { return e.ActualValue; }")] // 5
    [InlineData("try { throw new InvalidOperationException(\"io\", new FormatException(\"inner\")); } catch (Exception e) { return e.InnerException.Message; }")] // "inner"
    [InlineData("try { throw new ArgumentException(\"bad\"); } catch (ArgumentException e) { return e.ParamName == null; }")]                 // true
    [InlineData("try { throw new AggregateException(new FormatException(\"f\")); } catch (AggregateException e) { return e.InnerException.Message; }")] // "f": the first inner one
    // An argument exception the runtime throws on .NET's behalf names its parameter too.
    [InlineData("try { new List<int> { 1 }.CopyTo(new int[1], -1); return \"no\"; } catch (ArgumentOutOfRangeException e) { return e.ParamName; }")] // "arrayIndex"
    public void AMember_ReadsWhatTheConstructorTook(string statements)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet(statements);
    }
}
