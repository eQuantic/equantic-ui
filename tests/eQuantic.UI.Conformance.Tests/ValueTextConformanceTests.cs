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
    // A NULL provider is the current culture, as .NET reads it.
    [InlineData("return string.Format((IFormatProvider?)null, \"{0:F1}|{1}\", 2.25, 3);")]                               // "2.3|3"
    [InlineData("return string.Format(null, \"{0}\", 2.5);")]                                                          // "2.5"
    [InlineData("double d = 2.5; return d.ToString((IFormatProvider?)null);")]                                         // "2.5"
    [InlineData("double d = 2.5; return d.ToString(\"F2\", null);")]                                                   // "2.50"
    [InlineData("return double.Parse(\"2.5\", (IFormatProvider?)null).ToString(System.Globalization.CultureInfo.InvariantCulture);")] // "2.5"
    // A bool's provider changes nothing, but C# evaluates it, after the receiver.
    [InlineData("var log = \"\"; bool B() { log += \"b\"; return true; } IFormatProvider? P() { log += \"p\"; return null; } var s = B().ToString(P()); return s + log;")] // "Truebp"
    [InlineData("bool? b = null; return \"[\" + b.ToString() + \"]\";")]                                               // "[]"
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
    // The params array passed as the array itself, as C# binds it: a covariant string[] and a
    // collection expression are the values, not one value.
    [InlineData("return string.Format(\"{0} e {1}\", new[] { \"a\", \"b\" });")]                                 // "a e b"
    [InlineData("var xs = new[] { \"a\", \"b\" }; return string.Format(\"{0}+{1}\", xs);")]                      // "a+b"
    [InlineData("var xs = new object[] { 1, 2 }; return string.Format(\"{0}+{1}\", xs);")]                         // "1+2"
    [InlineData("return string.Format(\"{0}|{1}\", [\"a\", \"b\"]);")]                                           // "a|b"
    [InlineData("return string.Format(System.Globalization.CultureInfo.InvariantCulture, \"{0} e {1}\", new[] { \"a\", \"b\" });")] // "a e b"
    [InlineData("return string.Format(\"{0}|{1}|{2}|{3}\", 1, 2, 3, 4);")]                                         // "1|2|3|4"
    // NAMED arguments out of their parameters' order: C# passes them by slot and evaluates them as
    // written.
    [InlineData("var log = \"\"; string T() { log += \"t\"; return \"{0}|{1}\"; } object A() { log += \"a\"; return 1; } object B() { log += \"b\"; return 2; } var s = string.Format(format: T(), arg1: B(), arg0: A()); return s + \"/\" + log;")] // "1|2/tba"
    [InlineData("var log = \"\"; object[] Args() { log += \"a\"; return new object[] { 1 }; } string T() { log += \"t\"; return \"{0}\"; } var s = string.Format(args: Args(), format: T()); return s + \"/\" + log;")] // "1/at"
    [InlineData("return string.Format(format: \"{0}\", arg0: 5);")]                                            // "5": control
    // An array written in place is its elements, each boxed as C# boxes it: a float keeps its digits.
    [InlineData("return string.Format(\"{0}\", new object[] { 0.1f });")]                                          // "0.1"
    [InlineData("float f = 0.1f; return string.Format(\"{0}|{1}\", new object[] { f, \"x\" });")]                   // "0.1|x"
    [InlineData("return string.Format(\"{0}|{1}\", [0.1f, 2]);")]                                                  // "0.1|2"
    public void AValue_IsWrittenAsDotNetWritesIt(string statements)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet(statements);
    }

    /// <summary>A bool's provider named by a bare identifier that binds to a PROPERTY: its getter
    /// runs, after the receiver, as C# runs it.</summary>
    [SkippableFact]
    public void ABoolsProvider_ThatIsAProperty_IsStillRead()
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet(
            "return new Probe().Run(true);",   // "Truep"
            """
            public record Probe
            {
                public string Log { get; set; } = "";
                public IFormatProvider? Provider { get { Log += "p"; return null; } }
                public string Run(bool b) { var s = b.ToString(Provider); return s + Log; }
            }
            """);
    }
}
