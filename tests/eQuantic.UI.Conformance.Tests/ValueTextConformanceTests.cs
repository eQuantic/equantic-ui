using eQuantic.UI.Conformance.Tests.Infrastructure;
using Xunit;

namespace eQuantic.UI.Conformance.Tests;

/// <summary>
/// A value is written as .NET writes it, on both sides, through every route to text: a bool's
/// <c>ToString()</c> (#381), <c>string.Format</c> with a format provider (#377), and a float or a
/// double through the formatter, where a number reaches <c>$eq.text.format</c> or
/// <c>$eq.text.stringFormat</c> with no way to say it is a single (#378).
/// </summary>
public class ValueTextConformanceTests
{
    [SkippableTheory]
    // ---- a bool's ToString (#381) ----
    [InlineData("bool b = false; return b.ToString();")]                                                   // "False"
    [InlineData("var a = new[] { true }; return a[0].ToString();")]                                        // "True"
    [InlineData("bool? n = null; return \"[\" + n.ToString() + \"]\";")]                                   // "[]"
    [InlineData("bool? n = true; return n.ToString();")]                                                   // "True"
    [InlineData("int x = 2; return (x > 1).ToString();")]                                                  // "True"
    // ---- string.Format with a format provider (#377) ----
    [InlineData("float g = 0.1f; return string.Format(System.Globalization.CultureInfo.InvariantCulture, \"{0}\", g);")] // "0.1"
    [InlineData("return string.Format(System.Globalization.CultureInfo.InvariantCulture, \"{0:F2}|{1}\", 1.5, \"x\");")]   // "1.50|x"
    [InlineData("return string.Format(System.Globalization.CultureInfo.CurrentCulture, \"{0}\", 2.5);")]                 // "2.5"
    // ---- a float and a double through the formatter (#378) ----
    [InlineData("float f = 1e9f; return $\"[{f,12}]\";")]                                                  // "[       1E+09]"
    [InlineData("float g = 0.1f; return $\"[{g,6}]\";")]                                                   // "[   0.1]"
    [InlineData("float g = 0.1f; return $\"[{g:G}]\";")]                                                   // "[0.1]"
    [InlineData("float g = 0.1f; return g.ToString(\"R\", System.Globalization.CultureInfo.InvariantCulture);")] // "0.1"
    [InlineData("float g = 0.1f; return g.ToString(\"G\", System.Globalization.CultureInfo.InvariantCulture);")] // "0.1"
    [InlineData("float g = 0.1f; return string.Format(\"{0}\", g);")]                                      // "0.1"
    [InlineData("float g = 0.1f; return string.Format(\"{0:G}\", g);")]                                    // "0.1"
    [InlineData("double d = 1e21; return string.Format(\"{0}\", d);")]                                     // "1E+21"
    [InlineData("double d = 1e21; return d.ToString(\"G\", System.Globalization.CultureInfo.InvariantCulture);")] // "1E+21"
    [InlineData("double d = 0.1 + 0.2; return d.ToString(\"R\", System.Globalization.CultureInfo.InvariantCulture);")] // "0.30000000000000004"
    [InlineData("float? f = 0.1f; return $\"[{f,6}]\";")]                                                  // "[   0.1]"
    [InlineData("float? f = null; return $\"[{f,4}]\";")]                                                  // "[    ]"
    // ---- string.Format's placeholders ----
    [InlineData("return string.Format(\"{0}\", true);")]                                                   // "True"
    [InlineData("return string.Format(\"[{0,5}]\", 42);")]                                                 // "[   42]"
    [InlineData("return string.Format(\"[{0,-5}]\", 42);")]                                                // "[42   ]"
    [InlineData("return string.Format(\"[{0,8:F2}]\", 3.14159);")]                                         // "[    3.14]"
    public void AValue_IsWrittenAsDotNetWritesIt(string statements)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet(statements);
    }
}
