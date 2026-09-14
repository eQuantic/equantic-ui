using System.Reflection;
using System.Text.RegularExpressions;
using eQuantic.UI.Native.Components;
using eQuantic.UI.Native.Framework;
using eQuantic.UI.Primitives;
using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// <c>docs/FLUTTER-PARITY.md</c>, held to the tree. Every row of that document claims something
/// about this SDK, and a document full of claims nobody checks is the arrangement this repo has
/// already retired once — the design handoff drifted in both directions for exactly that reason.
///
/// <para>
/// So each row carries a verdict and each verdict is an assertion. SAME, DIFFERENT and PARTIAL say
/// "we have this", and the probe must find it. GAP says "we do not", and the probe must NOT — a
/// baseline that may only shrink, so closing a gap fails this test and makes someone update the
/// row rather than leaving the document claiming an absence that ended.
/// </para>
///
/// <para>
/// And every row must HAVE a probe. That is the half that keeps the document honest as it grows: a
/// claim added without one fails here, so the audit cannot quietly gain unverified prose.
/// </para>
/// </summary>
public class FlutterParityPinTests
{
    // ---- the document ----------------------------------------------------------------------------

    private sealed record Row(string Feature, string Verdict, int Line);

    private static readonly IReadOnlyList<Row> Rows = ReadRows();

    private static IReadOnlyList<Row> ReadRows()
    {
        var here = new DirectoryInfo(AppContext.BaseDirectory);
        while (here is not null && !File.Exists(Path.Combine(here.FullName, "docs", "FLUTTER-PARITY.md")))
            here = here.Parent;
        here.Should().NotBeNull("the audit is the subject of this suite — a missing file is a failure");

        var rows = new List<Row>();
        var lines = File.ReadAllLines(Path.Combine(here!.FullName, "docs", "FLUTTER-PARITY.md"));
        for (var i = 0; i < lines.Length; i++)
        {
            var cells = lines[i].Split('|');
            // A data row: leading pipe, three cells, and a bolded verdict in the third.
            if (cells.Length < 5 || !lines[i].StartsWith('|')) continue;
            var verdict = Regex.Match(cells[3], @"\*\*(SAME|DIFFERENT|PARTIAL|GAP)");
            if (!verdict.Success) continue;
            rows.Add(new Row(Key(cells[1]), verdict.Groups[1].Value, i + 1));
        }
        return rows;
    }

    /// <summary>The row's identity: its first backticked symbol, else its first three words. Stable
    /// enough to key on, and brittle in the direction that helps — reword the claim and the pin asks
    /// to be revisited with it.</summary>
    private static string Key(string cell)
    {
        var tick = Regex.Match(cell, "`([^`]+)`");
        if (tick.Success) return tick.Groups[1].Value.Trim();
        var words = cell.Trim().Trim('"').Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return string.Join(' ', words.Take(3));
    }

    // ---- the tree --------------------------------------------------------------------------------

    private static readonly Assembly[] Surface =
    [
        typeof(VisualNode).Assembly,                    // Primitives — the vocabulary
        typeof(eQuantic.UI.Components.Button).Assembly, // the component library
        typeof(LayoutNode).Assembly,                    // Native.Framework — layout
        typeof(PhotonHost).Assembly,                    // Native.Components — realizer, semantics
    ];

    private static Type? Find(string name) => Surface
        .SelectMany(a => a.GetExportedTypes())
        .FirstOrDefault(t => t.Name == name || t.Name == name + "Attribute");

    private static bool Has(string type) => Find(type) is not null;

    /// <summary>
    /// A member a CONSUMER can reach: public, or protected and therefore reachable by the subclass
    /// they write. Private is excluded deliberately — this file claims capabilities, and a probe
    /// satisfied by an implementation detail is a claim that passes for the wrong reason.
    /// </summary>
    private static bool HasMember(string type, string member) =>
        Find(type) is { } t && t.GetMember(member,
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static)
            .Any(Reachable);

    private static bool Reachable(MemberInfo member) => member switch
    {
        MethodBase method => method.IsPublic || method.IsFamily || method.IsFamilyOrAssembly,
        PropertyInfo property => property.GetMethod is { } getter
            && (getter.IsPublic || getter.IsFamily || getter.IsFamilyOrAssembly),
        FieldInfo field => field.IsPublic || field.IsFamily || field.IsFamilyOrAssembly,
        _ => true,
    };

    /// <summary>NOTHING in the public surface answers to this name — the shape a GAP takes.</summary>
    private static bool Nothing(params string[] names) => names.All(n => !Has(n));

    // ---- what each row claims ---------------------------------------------------------------------

    private static readonly Dictionary<string, Func<bool>> Probes = new()
    {
        // 1 — trees and lifecycle
        ["Widget"] = () => Has("VisualNode") && !HasMember("VisualNode", "Parent"),
        ["StatelessWidget"] = () => Has("StatelessComponent"),
        ["StatefulWidget"] = () => HasMember("StatefulComponent", "SetState"),
        ["initState"] = () => HasMember("StatefulComponent", "OnMount"),
        ["dispose"] = () => HasMember("StatefulComponent", "OnUnmount"),
        ["didUpdateWidget"] = () => HasMember("UiComponent", "AdoptConfig"),
        ["didChangeDependencies"] = () => Nothing("DependencyScope", "InheritedScope"),
        ["InheritedWidget"] = () => HasMember("UiComponent", "GetService"),
        ["Element"] = () => Nothing("Element", "BuildContext", "ElementTree"),
        ["RenderObject"] = () => HasMember("LayoutNode", "Parent"),
        ["parentData"] = () => !HasMember("LayoutNode", "ParentData") && HasMember("LayoutNode", "Bounds"),
        ["RenderSliver"] = () => Has("ListView") && Nothing("Sliver", "SliverList"),
        // The shapes: FlexNode is the one base the vocabulary names; nothing names the single-child one.
        ["SingleChildRenderObjectWidget"] = () => Has("FlexNode") && Nothing("SingleChildNode", "WrapperNode", "ProxyNode"),

        // 2 — layout
        ["Constraints go down,"] = () => Has("LayoutContext"),
        ["BoxConstraints"] = () => Nothing("BoxConstraints", "Constraints"),
        // Geometry is the vocabulary's own, which is the whole claim: the three types ship in the
        // SAME assembly as VisualNode, so a realizer that has never heard of Photon can spell a box.
        // Asserted by ASSEMBLY rather than by name — `Has` would pass on a second copy declared
        // anywhere in the surface, and a second copy is the defect this row exists to have ended.
        ["Rect"] = () => new[] { typeof(Rect), typeof(Point), typeof(Size) }
            .All(t => t.Assembly == typeof(VisualNode).Assembly),
        ["LayoutBuilder"] = () => Nothing("LayoutBuilder", "SizeBuilder") && Has("AdaptiveNode"),
        ["CustomMultiChildLayout"] = () => Nothing("MultiChildLayoutDelegate", "LayoutDelegate"),
        ["CustomSingleChildLayout"] = () => Nothing("SingleChildLayoutDelegate"),
        ["RenderBox"] = () => Nothing("RenderBox"),
        ["CustomPaint"] = () => Has("Canvas") && HasMember("ICanvasPainter", "Width"),
        ["Canvas"] = () => Nothing("FragmentProgram", "FragmentShader"),
        // The cut is reported (MaxLines exists, MeasuredLine.Ellipsized is written); no neutral type owns the mark.
        ["TextOverflow.ellipsis"] = () => HasMember("Text", "MaxLines") && Nothing("TextOverflow", "TextPainter"),

        // 3 — state
        ["setState"] = () => HasMember("StatefulComponent", "StateInvalidated"),
        ["ValueNotifier"] = () => Nothing("ValueNotifier", "ValueListenableBuilder"),
        ["ChangeNotifier"] = () => Nothing("ChangeNotifier", "ListenableBuilder"),
        ["InheritedNotifier"] = () => Nothing("InheritedNotifier"),
        ["FutureBuilder"] = () => Has("IServerPrefetch") && Nothing("FutureBuilder", "StreamBuilder"),

        // 4 — animation
        ["AnimationController"] = () => Has("IFrameTicker") && Has("LoopMotion") && Has("Presence"),
        ["Tween"] = () => Nothing("Tween", "ColorTween"),
        ["Curves"] = () => Has("Curve") && HasMember("Motion", "FastMs"),
        ["AnimatedContainer"] = () => Has("TransitionSpec"),
        ["AnimatedBuilder"] = () => Nothing("AnimatedBuilder", "AnimatedWidget"),
        ["Hero"] = () => Nothing("Hero", "SharedElement"),
        ["SpringSimulation"] = () => Has("SpringSpec") && Nothing("FrictionSimulation"),

        // 5 — gestures and focus
        ["Listener"] = () => HasMember("Canvas", "OnPointerDown"),
        ["HitTestBehavior"] = () => Nothing("HitTestBehavior"),
        ["GestureArena"] = () => Nothing("GestureArena", "GestureRecognizer"),
        ["GestureDetector"] = () => Has("Pressable") && Has("Draggable") && Has("Hoverable")
            && Has("Adjustable") && Has("Navigable"),
        ["RawGestureDetector"] = () => Nothing("RawGestureDetector"),
        ["FocusNode"] = () => HasMember("Pressable", "InitialFocus") && Nothing("FocusNode", "FocusScope"),
        ["Shortcuts"] = () => Has("Shortcut") && Has("KeyChord"),
        // The controllers are shared; the protocol that drives them is a HOST method.
        ["TextEditingController"] = () => Has("CodeEditorController") && Has("SheetController")
            && HasMember("PhotonHost", "TextInput"),

        // 6 — routing
        ["Navigator.push/pop"] = () => Has("Navigator"),
        ["onGenerateRoute"] = () => Has("Page") && Nothing("RouteFactory"),
        ["RouterDelegate"] = () => Nothing("RouterDelegate", "RouteInformationParser"),
        ["PageRouteBuilder"] = () => Has("Presence"),

        // 7 — interop
        ["MethodChannel"] = () => Has("ICamera") && Has("IBiometrics") && Nothing("MethodChannel"),
        ["dart:ffi"] = () => Nothing("Ffi", "NativeLibrary"),
        ["AndroidView"] = () => Has("WebFrame") && Nothing("PlatformView"),

        // 8 — concurrency
        ["Isolate.spawn"] = () => Nothing("Isolate", "IsolatePort"),
        ["compute()"] = () => Nothing("Compute"),
        ["Marshalling back to"] = () => Has("IUiDispatcher"),

        // 9 — a11y, i18n, platform
        ["Semantics"] = () => Has("SemanticsTree") && Has("SemanticNode"),
        // A LOCATION probe: the row's claim is that the role enum sits in one target's assembly.
        // Moving it to Primitives turns this false and fails the PARTIAL row until it is rewritten.
        ["SemanticsNode"] = () => typeof(SemanticRole).Assembly == typeof(PhotonHost).Assembly
            && typeof(SemanticNode).Assembly == typeof(PhotonHost).Assembly,
        ["MergeSemantics"] = () => Nothing("MergeSemantics", "ExcludeSemantics"),
        ["Localizations"] = () => Has("ICultureController") && Nothing("LocalizationsDelegate"),
        ["TextDirection.ltr/rtl"] = () => Nothing("TextDirection"),
        ["ThemeData"] = () => Has("IAppTheme") && Has("Density") && Nothing("CupertinoThemeData"),
        ["MediaQuery"] = () => Has("SafeArea") && Nothing("MediaQuery"),

        // 10 — lifecycle and windows
        ["WidgetsBindingObserver"] = () => Nothing("WidgetsBindingObserver", "AppLifecycleState"),
        // One host, no contract between it and the shells, none of Flutter's per-concern bindings.
        ["WidgetsFlutterBinding"] = () => Has("PhotonHost") && Nothing("IPhotonHost", "FocusManager",
            "GestureBinding", "SchedulerBinding", "ServicesBinding", "PaintingBinding", "SemanticsBinding",
            "RendererBinding", "WidgetsBinding"),
        ["View"] = () => Has("WindowChrome"),
        ["devicePixelRatio"] = () => Nothing("DevicePixelRatio"),
    };

    // ---- the assertions ---------------------------------------------------------------------------

    [Fact]
    public void TheAuditHasRows_AndThisReadThem()
    {
        Rows.Should().HaveCountGreaterThan(40,
            "a pin that parsed nothing passes for the wrong reason — the audit is ten sections long");
    }

    /// <summary>
    /// The half that keeps the document honest as it GROWS. A row added without a probe is a claim
    /// nobody checks, which is what this instrument exists to make impossible.
    /// </summary>
    [Fact]
    public void EveryRow_IsProbed()
    {
        var unprobed = Rows.Where(r => !Probes.ContainsKey(r.Feature))
            .Select(r => $"line {r.Line}: {r.Feature}")
            .ToArray();

        unprobed.Should().BeEmpty("every claim in FLUTTER-PARITY.md owes a probe:"
            + $"{Environment.NewLine}  {string.Join(Environment.NewLine + "  ", unprobed)}{Environment.NewLine}");
    }

    [Fact]
    public void WhatWeClaimToHave_IsThere()
    {
        var broken = Rows.Where(r => r.Verdict != "GAP")
            .Where(r => Probes.TryGetValue(r.Feature, out var probe) && !probe())
            .Select(r => $"line {r.Line}: {r.Feature} is documented {r.Verdict} and the probe cannot find it")
            .ToArray();

        broken.Should().BeEmpty($"{Environment.NewLine}  "
            + string.Join(Environment.NewLine + "  ", broken) + Environment.NewLine);
    }

    /// <summary>
    /// The baseline that may only SHRINK. A gap closing is good news and still fails here, on
    /// purpose: the document would otherwise keep claiming an absence that ended, and the next
    /// reader would design around something we already have.
    /// </summary>
    [Fact]
    public void WhatWeCallAGap_IsStillMissing()
    {
        var closed = Rows.Where(r => r.Verdict == "GAP")
            .Where(r => Probes.TryGetValue(r.Feature, out var probe) && probe() == false)
            .Select(r => $"line {r.Line}: {r.Feature}")
            .ToArray();

        // A GAP probe answers TRUE while the gap is open — it asserts the absence.
        closed.Should().BeEmpty("a gap has closed; update FLUTTER-PARITY.md and move the row off GAP:"
            + $"{Environment.NewLine}  {string.Join(Environment.NewLine + "  ", closed)}{Environment.NewLine}");
    }
}
