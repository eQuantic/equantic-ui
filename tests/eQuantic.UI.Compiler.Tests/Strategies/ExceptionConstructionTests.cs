using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Compiler.Tests.Strategies;

/// <summary>
/// A framework exception's text where its message is absent or null is read from .NET itself, for
/// the constructor the call binds, and handed with the creation: the runtime keeps only
/// <c>Exception.Message</c>'s own, which names the type. A table of each type's text gave a
/// <c>TaskCanceledException</c> its base's, and could not say that <c>new SystemException()</c> and
/// <c>new SystemException(null)</c> write different texts. What each writes is proved on both sides by
/// ExceptionMessageConformanceTests.
/// </summary>
public class ExceptionConstructionTests
{
    private const string InvalidOperation = "['System.InvalidOperationException', 'System.SystemException', 'System.Exception']";

    [Fact]
    public void NoMessage_HandsTheTextOfItsConstructor()
    {
        TestHelper.ConvertExpression("new InvalidOperationException()").Should().Be(
            $"$eq.exceptions.create({InvalidOperation}, 'Operation is not valid due to the current state of the object.')");
        TestHelper.ConvertExpression("new System.Threading.Tasks.TaskCanceledException()").Should().Be(
            "$eq.exceptions.create(['System.Threading.Tasks.TaskCanceledException', 'System.OperationCanceledException', "
            + "'System.SystemException', 'System.Exception'], 'A task was canceled.')");
        TestHelper.ConvertExpression("new ArgumentNullException(\"x\")").Should().Be(
            "$eq.exceptions.create(['System.ArgumentNullException', 'System.ArgumentException', 'System.SystemException', "
            + "'System.Exception'], 'Value cannot be null.', { paramName: 'x' })");
    }

    [Fact]
    public void AMessageThatMayBeNull_FallsBackToIt()
    {
        TestHelper.ConvertExpression("new InvalidOperationException(str)").Should().Be(
            $"$eq.exceptions.create({InvalidOperation}, this.str ?? 'Operation is not valid due to the current state of the object.')");
        TestHelper.ConvertExpression("new ArgumentException(null, \"x\")").Should().Be(
            "$eq.exceptions.create(['System.ArgumentException', 'System.SystemException', 'System.Exception'], "
            + "'Value does not fall within the expected range.', { paramName: 'x' })");
    }

    [Fact]
    public void AMessageThatCannotBeNull_IsAllThereIs()
    {
        TestHelper.ConvertExpression("new InvalidOperationException($\"a{Id}\")").Should().Be(
            $"$eq.exceptions.create({InvalidOperation}, `a${{this.id}}`)");
        TestHelper.ConvertExpression("new InvalidOperationException(\"a\" + str)").Should().Be(
            $"$eq.exceptions.create({InvalidOperation}, 'a' + (this.str ?? ''))");
    }

    /// <summary>Where .NET's text is <c>Exception.Message</c>'s own, nothing is handed: the runtime
    /// writes it, naming the type. Which text it is depends on the constructor.</summary>
    [Fact]
    public void ExceptionMessagesOwnText_IsLeftToTheRuntime()
    {
        TestHelper.ConvertExpression("new Exception()").Should().Be("$eq.exceptions.create(['System.Exception'])");
        TestHelper.ConvertExpression("new SystemException()").Should().Be(
            "$eq.exceptions.create(['System.SystemException', 'System.Exception'], 'System error.')");
        TestHelper.ConvertExpression("new SystemException(str)").Should().Be(
            "$eq.exceptions.create(['System.SystemException', 'System.Exception'], this.str)");
    }

    /// <summary>A type initializer's text is composed from its type's name, which .NET's member reads
    /// as <c>TypeName</c>.</summary>
    [Fact]
    public void ATypeInitializer_CarriesItsTypeName()
    {
        TestHelper.ConvertExpression("new TypeInitializationException(\"T\", null)").Should().Be(
            "$eq.exceptions.create(['System.TypeInitializationException', 'System.SystemException', 'System.Exception'], "
            + "undefined, { innerException: null, typeName: 'T' })");
    }

    /// <summary>Eleven inner exceptions are eleven parts, the last of them hole <c>{10}</c>.</summary>
    [Fact]
    public void ElevenInnerExceptions_AreElevenArguments()
    {
        var inner = string.Join(", ", Enumerable.Range(0, 11).Select(index => $"new Exception(\"{index}\")"));
        TestHelper.ConvertExpression($"new AggregateException({inner})").Should()
            .EndWith("$eq.exceptions.create(['System.Exception'], '10')] })").And.NotContain("{10}");
    }
}
