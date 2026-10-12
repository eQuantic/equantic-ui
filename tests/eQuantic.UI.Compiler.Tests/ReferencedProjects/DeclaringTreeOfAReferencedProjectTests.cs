using FluentAssertions;

namespace eQuantic.UI.Compiler.Tests.ReferencedProjects;

/// <summary>
/// The places that read a symbol's DECLARATION ask for the model of the tree that declares it. That
/// tree used to be one of the compilation in hand, always. A type of a referenced project is declared
/// in that project's compilation, and asking the app's for the model of its file throws
/// <see cref="ArgumentException"/>: measured in Roslyn before this was written. So each of them asks
/// for the model of a tree from the compilation that holds it (<c>Compilation.ModelOf</c>), and each
/// has a case here, a component of the app's that reaches the place through a type of the library.
/// <para>
/// One build, a component a place, so a place that throws takes its own module with it and no other.
/// </para>
/// </summary>
public sealed class DeclaringTreeOfAReferencedProjectTests : IClassFixture<DeclaringTreeOfAReferencedProjectTests.BuiltOnce>
{
    private readonly Built _built;

    public DeclaringTreeOfAReferencedProjectTests(BuiltOnce once) => _built = once.Built;

    public sealed class BuiltOnce : IDisposable
    {
        private readonly Solution _solution = new Solution()
            .Library("Common", files:
            [
                ("Glyphs.cs",
                    """
                    using eQuantic.UI.Primitives;

                    namespace Common;

                    public static class Glyphs
                    {
                        public static IconGlyph Tick => new("tick", "M5 12l4 4L19 7", IconGlyphStyle.Stroke);
                    }
                    """),
                ("IDescribed.cs",
                    """
                    namespace Common;

                    public interface IDescribed
                    {
                        string Name { get; }

                        string Describe() => Framed("I am " + Name);

                        // A helper of the interface's own: it travels with the default that calls it,
                        // and which default calls it is read with the model of THIS file.
                        private string Framed(string text) => "[" + text + "]";
                    }
                    """),
                ("Counter.cs",
                    """
                    namespace Common;

                    public struct Counter
                    {
                        public int Value;

                        public void Bump() => Value++;
                    }
                    """),
                ("Bag.cs",
                    """
                    using System.Collections;

                    namespace Common;

                    public class Bag : IEnumerable<int>
                    {
                        private IEnumerator<int> Items()
                        {
                            yield return 1;
                            yield return 2;
                        }

                        // It hands back what another member answers, which is what the build reads.
                        public IEnumerator<int> GetEnumerator() => Items();

                        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
                    }
                    """),
            ])
            .App("App", references: ["Common"], globalUsings: SharedLibrary.Factories, files:
            [
                ("Screens/GlyphUser.cs",
                    """
                    using eQuantic.UI.Primitives;
                    using Common;

                    namespace App.Screens;

                    public sealed class GlyphUser : StatelessComponent
                    {
                        public override VisualNode Build(ComponentContext context) => Icon(Glyphs.Tick);
                    }
                    """),
                ("Screens/Described.cs",
                    """
                    using Common;

                    namespace App.Screens;

                    public sealed class Described : IDescribed
                    {
                        public string Name => "app";
                    }
                    """),
                ("Screens/CounterUser.cs",
                    """
                    using eQuantic.UI.Primitives;
                    using Common;

                    namespace App.Screens;

                    public sealed class CounterUser : StatefulComponent
                    {
                        private Counter _counter;

                        public override VisualNode Build(ComponentContext context) =>
                            Column(gap: Space.S2, children: [
                                Button("Bump", onPressed: () => SetState(() => _counter.Bump())),
                                Text($"{_counter.Value}", TypeRole.BodyM, context.Theme.TextPrimary, maxLines: 1),
                            ]);
                    }
                    """),
                ("Screens/Deck.cs",
                    """
                    using System.Collections;
                    using Common;

                    namespace App.Screens;

                    // The app's class implements the plain interface again, over the library's
                    // sequence: whether its member answers as the library's is read off BOTH bodies.
                    public sealed class Deck : Bag, IEnumerable
                    {
                        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
                    }
                    """),
                ("Screens/DeckUser.cs",
                    """
                    using eQuantic.UI.Primitives;

                    namespace App.Screens;

                    public sealed class DeckUser : StatelessComponent
                    {
                        public override VisualNode Build(ComponentContext context)
                        {
                            var total = 0;
                            foreach (var item in new Deck()) total += item;
                            return Text($"{total}", TypeRole.BodyM, context.Theme.TextPrimary, maxLines: 1);
                        }
                    }
                    """),
            ]);

        public BuiltOnce() => Built = _solution.Build();

        internal Built Built { get; }

        public void Dispose() => _solution.Dispose();
    }

    [Fact]
    public void TheBuildSucceeds_AndSaysNothing()
    {
        _built.Failed.Should().BeFalse(_built.Errors);
        _built.Errors.Should().BeEmpty();
    }

    [Fact]
    public void AGlyphOfTheLibrary_IsInlinedFromItsInitializer()
    {
        // The initializer is a node of the library's file, converted where the page reads it.
        _built.Module("GlyphUser").Should().Contain("'tick'").And.Contain("'M5 12l4 4L19 7'");
        _built.SiblingImportsOf("GlyphUser").Should().NotContain("Glyphs",
            "an inlined glyph leaves no reference to the class that held it");
    }

    [Fact]
    public void ADefaultOfTheLibrarysInterface_IsWrittenIntoTheClassThatTakesIt()
    {
        // The default's body lives in the interface's file, in the library's compilation, and so
        // does the private helper it calls, which is found by binding the call in that file.
        _built.Module("Described").Should().Contain("describe()").And.Contain("this.framed('I am ' + this.name)")
            .And.Contain("framed(text");
    }

    [Fact]
    public void AStructOfTheLibrary_IsReadForTheMethodsThatWriteIt()
    {
        // Whether Bump writes the struct is read off its body, which is in the library's file.
        _built.Module("CounterUser").Should().Contain("this._counter.bump()");
        _built.SiblingImportsOf("CounterUser").Should().Contain("Counter");
    }

    [Fact]
    public void ASequenceOfTheLibrary_IsReadForWhatItsEnumeratorsHandBack()
    {
        // Whether Deck's own GetEnumerator answers as the one it inherits is read off the bodies
        // of both, and the inherited one's is in the library's file.
        _built.Module("Deck").Should().Contain("export class Deck extends Bag");
        _built.SiblingImportsOf("Deck").Should().Contain("Bag");
        _built.Module("Bag").Should().Contain("export class Bag");
        _built.SiblingImportsOf("DeckUser").Should().Contain("Deck");
    }
}
