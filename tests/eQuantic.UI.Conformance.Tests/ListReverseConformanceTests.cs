using eQuantic.UI.Conformance.Tests.Infrastructure;
using Xunit;

namespace eQuantic.UI.Conformance.Tests;

/// <summary>
/// <c>List&lt;T&gt;.Reverse</c> and <c>Array.Reverse</c> reverse in place, a range of them too, and check their
/// arguments as .NET's do, on both sides (#1076). A list's own <c>Reverse</c> was LINQ's, a reversed copy thrown
/// away, so the list stayed as it was, and neither read a range: the snippet engine (#1013), which reverses
/// the copies it made from the last caret to the first, placed each copy's stops at another caret. LINQ's
/// <c>Reverse</c> still leaves its source alone.
/// </summary>
public class ListReverseConformanceTests
{
    [SkippableTheory]
    [InlineData("var l = new List<int> { 1, 2, 3 }; l.Reverse(); return string.Join(\",\", l);")]                                      // 3,2,1
    [InlineData("var l = new List<int> { 1, 2, 3, 4, 5 }; l.Reverse(1, 3); return string.Join(\",\", l);")]                          // 1,4,3,2,5
    [InlineData("var l = new List<int> { 1, 2, 3, 4 }; l.Reverse(0, 0); l.Reverse(3, 1); return string.Join(\",\", l);")]             // 1,2,3,4
    [InlineData("var l = new List<string> { \"a\", \"b\" }; var r = ((IEnumerable<string>)l).Reverse().ToList(); return string.Join(\",\", l) + \"|\" + string.Join(\",\", r);")] // a,b|b,a
    [InlineData("var a = new[] { 1, 2, 3 }; Array.Reverse(a); return string.Join(\",\", a);")]                                         // 3,2,1
    [InlineData("var a = new[] { 1, 2, 3, 4, 5 }; Array.Reverse(a, 1, 3); return string.Join(\",\", a);")]                           // 1,4,3,2,5
    [InlineData("var a = new[] { 1, 2, 3 }; var r = a.Reverse().ToList(); return string.Join(\",\", a) + \"|\" + string.Join(\",\", r);")] // 1,2,3|3,2,1
    public void Reverse_AnswersAsDotNet(string statements)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet(statements);
    }

    [SkippableTheory]
    [InlineData("try { var l = new List<int> { 1, 2, 3 }; l.Reverse(2, 2); return \"no\"; } catch (Exception e) { return e.Message; }")]
    [InlineData("try { var l = new List<int> { 1, 2, 3 }; l.Reverse(-1, 1); return \"no\"; } catch (Exception e) { return e.Message; }")]
    [InlineData("try { var l = new List<int> { 1, 2, 3 }; l.Reverse(0, -1); return \"no\"; } catch (Exception e) { return e.Message; }")]
    [InlineData("try { var a = new[] { 1, 2, 3 }; Array.Reverse(a, 1, 3); return \"no\"; } catch (Exception e) { return e.Message; }")]
    [InlineData("try { var a = new[] { 1, 2, 3 }; Array.Reverse(a, 0, -2); return \"no\"; } catch (Exception e) { return e.Message; }")]
    [InlineData("try { int[] a = null; Array.Reverse(a); return \"no\"; } catch (Exception e) { return e.Message; }")]
    public void ReverseRefuses_AsDotNetDoes(string statements)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet(statements);
    }
}
