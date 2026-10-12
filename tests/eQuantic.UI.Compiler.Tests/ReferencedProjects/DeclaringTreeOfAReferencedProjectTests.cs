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

                        string Describe() => "I am " + Name;
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

                    public sealed class Bag : IEnumerable<int>
                    {
                        public IEnumerator<int> GetEnumerator()
                        {
                            yield return 1;
                            yield return 2;
                        }

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
                ("Screens/BagUser.cs",
                    """
                    using eQuantic.UI.Primitives;
                    using Common;

                    namespace App.Screens;

                    public sealed class BagUser : StatelessComponent
                    {
                        public override VisualNode Build(ComponentContext context)
                        {
                            var total = 0;
                            foreach (var item in new Bag()) total += item;
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
        // The default's body lives in the interface's file, in the library's compilation.
        _built.Module("Described").Should().Contain("describe()").And.Contain("'I am ' + this.name");
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
        // How the class is walked is read off its GetEnumerator members, in the library's file.
        _built.Module("BagUser").Should().NotBeNull();
        _built.SiblingImportsOf("BagUser").Should().Contain("Bag");
        _built.Module("Bag").Should().Contain("export class Bag");
    }
}
