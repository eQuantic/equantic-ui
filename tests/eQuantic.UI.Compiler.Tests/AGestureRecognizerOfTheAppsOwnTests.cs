using System.Runtime.CompilerServices;
using eQuantic.UI.Compiler.Tests.ReferencedProjects;
using FluentAssertions;

namespace eQuantic.UI.Compiler.Tests;

/// <summary>
/// A gesture of the app's own crosses to the browser (#1563): an app's subclass of the vocabulary's
/// <c>GestureRecognizer</c> is a client class like any other, and a component hosts it with a
/// <c>RawGestureDetector</c> and a <c>GestureRecognizerFactory&lt;T&gt;</c>. Built as an app is built, from the
/// dashboard's own <c>HoldThenDrag.cs</c>, the recognizer the sample ships: fields, nullable delegates, a deadline,
/// a resolution, and an override of every virtual a long press needs.
/// </summary>
public class AGestureRecognizerOfTheAppsOwnTests
{
    private const string Cards =
        """
        using eQuantic.UI.Primitives;

        namespace eQuantic.Console;

        public sealed class Cards : StatefulComponent
        {
            private Point _offset;

            private void Move(Point offset) => SetState(() => _offset = offset);

            public override VisualNode Build(ComponentContext context) =>
                RawGestureDetector(Text($"{_offset.X}", TypeRole.BodyM),
                    [new GestureRecognizerFactory<HoldThenDrag>(() => new(), drag => drag.OnMoved = Move)],
                    behavior: HitTestBehavior.Opaque);
        }
        """;

    private static string Sample([CallerFilePath] string here = "") =>
        File.ReadAllText(Path.GetFullPath(Path.Combine(Path.GetDirectoryName(here)!, "..", "..", "samples",
            "DefaultUIDashboard", "Gestures", "HoldThenDrag.cs")));

    [Fact]
    public void TheSamplesRecognizer_AndTheDetectorThatHostsIt_CompileForTheBrowser()
    {
        using var solution = new Solution()
            .App("Console", globalUsings: SharedLibrary.Factories,
                files: [("Gestures/HoldThenDrag.cs", Sample()), ("Components/Cards.cs", Cards)]);

        var built = solution.Build();

        built.Errors.Split('\n').Where(line => line.Contains(": error ", StringComparison.Ordinal)
                || line.Contains(": warning ", StringComparison.Ordinal))
            .Should().BeEmpty("a recognizer is ordinary client C#, and eqc translates every construct it uses");
        var recognizer = built.Module("HoldThenDrag");
        recognizer.Should().Contain("class HoldThenDrag extends GestureRecognizer", "the app's recognizer derives from the runtime's");
        recognizer.Should().MatchRegex(@"import \{[^}]*\bGestureRecognizer\b[^}]*\} from ""@equantic/runtime""",
            "the base it extends is the runtime's twin");
        recognizer.Should().Contain("startDeadline(500)", "the platforms' long press is a constant the C# names, which crosses as its value");
        foreach (var member in new[] { "isPointerAllowed(", "addAllowedPointer(", "handleEvent(", "didExceedDeadline(", "acceptGesture(", "rejectGesture(" })
            recognizer.Should().Contain(member, "every override the recognizer writes crosses");
        built.Module("Cards").Should().Contain("new GestureRecognizerFactory(", "a component hosts the recognizer through its factory");
    }
}
