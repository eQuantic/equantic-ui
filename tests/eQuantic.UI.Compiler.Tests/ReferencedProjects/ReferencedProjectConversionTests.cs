using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Compiler.Tests.ReferencedProjects;

/// <summary>
/// A value is converted where C# converts it, whatever it was written from (#833). The reader of a
/// module's names leaves out a constant that is written as its value, and <c>nameof</c>, because
/// nothing of what they NAME is written. The value written in their place is still handed to the
/// user-defined conversion C# applies there, and that call names a type: the module wrote
/// <c>Slot.fromInt(1)</c> and imported no <c>Slot</c>.
/// <para>
/// It shows where nothing else names the type: a caller whose symbol carries no parameter type, which
/// is an indexer and a constructor initializer. A field handed to the same indexer is the control,
/// and always imported it.
/// </para>
/// </summary>
public class ReferencedProjectConversionTests
{
    /// <summary>Two values of the library that convert, the two indexers that take them, a class
    /// built from one, and a constant.</summary>
    private const string Common =
        """
        namespace Common;

        public readonly record struct Slot(int Index)
        {
            public static implicit operator Slot(int index) => new(index);
        }

        public readonly record struct Tag(string Name)
        {
            public static implicit operator Tag(string name) => new(name);
        }

        public static class Limits
        {
            public const int First = 1;
        }

        public sealed class Board
        {
            public string this[Slot slot] => "cell " + slot.Index;
        }

        public sealed class Shelf
        {
            public string this[Tag tag] => "tag " + tag.Name;
        }

        public class Entry
        {
            public Entry(Slot slot) { Slot = slot; }

            public Slot Slot { get; }
        }
        """;

    [Theory]
    [InlineData("a field", "_board[_index]", "Slot.fromInt(this._index)", "Slot")]
    [InlineData("a constant", "_board[Common.Limits.First]", "Slot.fromInt(1)", "Slot")]
    [InlineData("nameof", "_shelf[nameof(Home)]", "Tag.fromString('Home')", "Tag")]
    public void AValueHandedToAnIndexerThatConvertsIt_ImportsTheTypeThatConverts(
        string what, string read, string written, string type)
    {
        using var solution = new Solution()
            .Library("Common", files: [("Types.cs", Common)])
            .App("App", references: ["Common"], globalUsings: SharedLibrary.Factories, files:
            [
                ("Screens/Home.cs",
                    $$"""
                    using eQuantic.UI.Primitives;

                    namespace App.Screens;

                    public sealed class Home : StatefulComponent
                    {
                        private readonly Common.Board _board = new();
                        private readonly Common.Shelf _shelf = new();
                        private int _index = 1;

                        public override VisualNode Build(ComponentContext context) =>
                            Text({{read}}, TypeRole.BodyM, context.Theme.TextPrimary, maxLines: 1);
                    }
                    """),
            ]);
        var built = solution.Build();

        built.Failed.Should().BeFalse(built.Errors);
        built.Module("Home").Should().Contain(written, $"{what} is converted where C# converts it");
        built.SiblingImportsOf("Home").Should().Contain(type, $"the module writes a call on {type}");
    }

    [Fact]
    public void AConstantHandedToABaseConstructorThatConvertsIt_ImportsTheTypeThatConverts()
    {
        // The other caller with no parameter type to read: `: base(...)` is no expression, so its
        // constructor is never asked for what it takes. A class of the app, which is a module of its
        // own, written by the emitter of classes.
        using var solution = new Solution()
            .Library("Common", files: [("Types.cs", Common)])
            .App("App", references: ["Common"], globalUsings: SharedLibrary.Factories, files:
            [
                ("Model/Opening.cs",
                    "namespace App.Model;\n\npublic sealed class Opening : Common.Entry\n{\n    public Opening() : base(Common.Limits.First) { }\n}\n"),
                ("Screens/Home.cs",
                    """
                    using eQuantic.UI.Primitives;
                    using App.Model;

                    namespace App.Screens;

                    public sealed class Home : StatelessComponent
                    {
                        public override VisualNode Build(ComponentContext context) =>
                            Text(new Opening().Slot.Index.ToString(), TypeRole.BodyM, context.Theme.TextPrimary, maxLines: 1);
                    }
                    """),
            ]);
        var built = solution.Build();

        built.Failed.Should().BeFalse(built.Errors);
        built.Module("Opening").Should().Contain("Slot.fromInt(1)");
        built.SiblingImportsOf("Opening").Should().Contain("Slot");
    }
}
