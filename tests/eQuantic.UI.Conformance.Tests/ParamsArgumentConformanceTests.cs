using eQuantic.UI.Conformance.Tests.Infrastructure;
using Xunit;

namespace eQuantic.UI.Conformance.Tests;

/// <summary>
/// A <c>params</c> parameter is a rest parameter on the other side, and C# fills it two ways: with the
/// elements a call lists, which it packs itself, or with a collection the call hands over whole, which
/// the rest parameter receives spread. The bound tree says which form a call took, and only it can: an
/// array of ints handed to <c>params object[]</c> is ONE element, and a list handed to
/// <c>params IReadOnlyList&lt;int&gt;</c> is the collection itself. Read from the argument's type, a
/// collection that was not an array arrived as one element, so <c>new FontFamily("Brand", [regular,
/// italic])</c> held one file, an array, in the browser (Copilot's second review of #1062).
/// </summary>
public class ParamsArgumentConformanceTests
{
    private const string Source = """
        using System.Collections.Generic;
        using eQuantic.UI.Primitives;

        public class Bag
        {
            public string Text = "";
            public Bag(params IReadOnlyList<int> xs) { foreach (var x in xs) Text += x; Text += "#" + xs.Count; }
        }

        public class ArrayBag
        {
            public string Text = "";
            public ArrayBag(params int[] xs) { foreach (var x in xs) Text += x; Text += "#" + xs.Length; }
        }

        public class TaggedBag
        {
            public string Text;
            public TaggedBag(string tag, params int[] xs) { Text = tag + ":"; foreach (var x in xs) Text += x; Text += "#" + xs.Length; }
        }

        public static class Tally
        {
            public static string Tagged(string tag, params int[] xs) { var text = tag + ":"; foreach (var x in xs) text += x; return text + "#" + xs.Length; }
            public static string Of(params IReadOnlyList<int> xs) { var text = ""; foreach (var x in xs) text += x; return text + "#" + xs.Count; }
            public static string OfArray(params int[] xs) { var text = ""; foreach (var x in xs) text += x; return text + "#" + xs.Length; }
            public static int Objects(params object[] xs) => xs.Length;
        }

        public static class Faces
        {
            public static readonly FontFile Regular = new("App", "Fonts/Brand.ttf", 400, 400);
            public static readonly FontFile Italic = new("App", "Fonts/Brand-Italic.ttf", 400, 400, Italic: true);
        }
        """;

    private static readonly (string Name, string Statements)[] AppCases =
    [
        ("a params collection's elements, listed", "return new Bag(1, 2, 3).Text + \"|\" + Tally.Of(1, 2, 3);"),
        ("a params collection written as a collection expression", "return new Bag([1, 2]).Text + \"|\" + Tally.Of([1, 2]);"),
        ("a list handed whole to a params collection", "var list = new List<int> { 4, 5 }; return new Bag(list).Text + \"|\" + Tally.Of(list);"),
        ("a params collection handed whole by name", "var list = new List<int> { 4, 5 }; return new Bag(xs: list).Text + \"|\" + Tally.Of(xs: [6, 7]);"),
        ("an array handed whole to a params array", "int[] xs = [6, 7]; return new ArrayBag(xs).Text + \"|\" + Tally.OfArray(xs);"),
        ("a params array written as a collection expression", "return new ArrayBag([8, 9]).Text + \"|\" + Tally.OfArray([8, 9]);"),
        ("no argument for a params parameter", "return new Bag().Text + \"|\" + new ArrayBag().Text + \"|\" + Tally.Of() + \"|\" + Tally.OfArray();"),
        ("an array of ints is one element of a params object[]", "int[] xs = [1, 2]; return Tally.Objects(xs);"),
        ("an array of strings is a params object[] itself", "string[] xs = [\"a\", \"b\"]; return Tally.Objects(xs);"),
        ("a named argument before the elements a call lists", "return Tally.Tagged(tag: \"t\", 1, 2, 3) + \"|\" + new TaggedBag(tag: \"b\", 4, 5).Text;"),
    ];

    /// <summary>A vocabulary type's twin is written by hand, and its constructor's rest parameter is
    /// filled as an app type's is.</summary>
    private static readonly (string Name, string Statements)[] VocabularyCases =
    [
        ("a family's files, listed", "var family = new FontFamily(\"Brand\", Faces.Regular, Faces.Italic); return family.Files.Count + \"|\" + family.Files[1].Resource;"),
        ("a family's files as a collection expression", "var family = new FontFamily(\"Brand\", [Faces.Regular, Faces.Italic]); return family.Files.Count + \"|\" + family.Files[1].Resource;"),
        ("a family built from another's files", "var family = new FontFamily(\"Brand\", Faces.Regular, Faces.Italic); var copy = new FontFamily(\"Copy\", family.Files); return copy.Files.Count + \"|\" + copy.Files[0].Resource;"),
        ("a family built where its type is the target", "FontFamily family = new(\"Brand\", [Faces.Regular, Faces.Italic]); return family.Files.Count + \"|\" + family.Files[0].Italic + family.Files[1].Italic;"),
        ("a family the machine has", "var family = new FontFamily(\"Georgia\"); return family.Files.Count + \"|\" + family.Name;"),
        ("a family's name by name, its files listed", "var family = new FontFamily(Name: \"Brand\", Faces.Regular, Faces.Italic); return family.Files.Count + \"|\" + family.Files[1].Resource;"),
    ];

    [SkippableTheory]
    [InlineData(true)]
    [InlineData(false)]
    public void AParamsParameterOfAnAppsType_TakesWhatCSharpHandsIt(bool typeAnnotations) =>
        ModuleGraph.AssertSameAsDotNet(Source, typeAnnotations, AppCases);

    [SkippableTheory]
    [InlineData(true)]
    [InlineData(false)]
    public void AParamsParameterOfTheVocabulary_TakesWhatCSharpHandsIt(bool typeAnnotations) =>
        ModuleGraph.AssertSameAsDotNet(Source, typeAnnotations, VocabularyCases);
}
