using System.Reflection;
using System.Runtime.CompilerServices;
using eQuantic.UI.Primitives;
using FluentAssertions;

namespace eQuantic.UI.Web.Tests;

/// <summary>
/// Every node kind the vocabulary declares has its children's slots in the element engine.
/// <para>
/// The engine reads a node's children in <c>ChildSlots</c>, one switch over <see cref="VisualNode.NodeKind"/>,
/// whose default throws naming the kind. It was a visitor, one <c>Visit</c> overload per kind, so a kind
/// added to the vocabulary failed the BUILD until it had a door; the engine now runs in the browser too,
/// where a class has one member per name (EQ1007), and that guarantee moves here.
/// </para>
/// <para>
/// It EXECUTES rather than reading the source: one instance of every concrete <see cref="VisualNode"/> in
/// Primitives, built with <see cref="FactoryArguments"/>, is mounted through an owner, the public way into
/// the engine. A kind with no arm fails it by name. Two kinds that share a <c>NodeKind</c> fail it too, on
/// the typed read of the arm that is not theirs. A kind no instance can be built for is named in
/// <c>child-slots-unmounted.baseline.txt</c>, which may only shrink: a new kind gets no entry, it gets an
/// instance, and the regenerator (<c>EQ_UPDATE_CHILD_SLOTS_BASELINE=1</c>) only takes lines out.
/// </para>
/// </summary>
public class ChildSlotsCoverageTests
{
    private static readonly ComponentContext Context = new(PhotonTheme.Instance);

    private static string RepoRoot([CallerFilePath] string sourcePath = "") =>
        Path.GetFullPath(Path.Combine(Path.GetDirectoryName(sourcePath)!, "..", ".."));

    private static string BaselinePath =>
        Path.Combine(RepoRoot(), "tests", "eQuantic.UI.Web.Tests", "child-slots-unmounted.baseline.txt");

    /// <summary>The kinds an engine meets: every concrete node type the vocabulary declares.</summary>
    private static IReadOnlyList<Type> ConcreteNodes() =>
        typeof(VisualNode).Assembly.GetTypes()
            .Where(type => typeof(VisualNode).IsAssignableFrom(type)
                && type is { IsAbstract: false, IsGenericTypeDefinition: false })
            .OrderBy(type => type.FullName, StringComparer.Ordinal)
            .ToList();

    /// <summary>An instance through the widest public constructor, as a factory mirrors it.</summary>
    private static bool TryBuild(Type type, out VisualNode? node, out string why)
    {
        node = null;
        var constructor = type.GetConstructors(BindingFlags.Public | BindingFlags.Instance)
            .OrderByDescending(candidate => candidate.GetParameters().Length)
            .FirstOrDefault();
        if (constructor is null)
        {
            why = "no public constructor";
            return false;
        }
        if (!FactoryArguments.For(constructor.GetParameters(), out var arguments, out why))
            return false;
        var parameters = constructor.GetParameters();
        // A constructor that validates can refuse a plausible argument. When it names the parameter, that
        // one argument is varied and the call tried again, a few times: no list of which kind wants what.
        for (var attempt = 0; ; attempt++)
        {
            try
            {
                node = (VisualNode)constructor.Invoke(arguments);
                return true;
            }
            catch (Exception thrown)
            {
                var refused = thrown is TargetInvocationException { InnerException: { } inner } ? inner : thrown;
                why = refused.Message;
                var index = refused is ArgumentException { ParamName: { } name }
                    ? Array.FindIndex(parameters, parameter => parameter.Name == name)
                    : -1;
                if (attempt == 3 || index < 0
                    || !FactoryArguments.TryVary(parameters[index].ParameterType, arguments[index], 0, out var varied))
                    return false;
                arguments[index] = varied;
            }
        }
    }

    /// <summary>
    /// The mount below reads Primitives' types, so a node kind declared anywhere else would never be held
    /// to an arm: every node type of the SDK's other assemblies takes its kind from Primitives.
    /// </summary>
    [Fact]
    public void NoNodeKindIsDeclaredOutsideTheVocabulary()
    {
        var vocabulary = typeof(VisualNode).Assembly;
        var outside = typeof(ChildSlotsCoverageTests).Assembly.GetReferencedAssemblies()
            .Where(name => name.Name is { } assembly && assembly.StartsWith("eQuantic.UI", StringComparison.Ordinal))
            .Select(Assembly.Load)
            .Where(assembly => assembly != vocabulary)
            .SelectMany(assembly => assembly.GetTypes())
            .Where(type => typeof(VisualNode).IsAssignableFrom(type) && !type.IsAbstract
                && type.GetProperty(nameof(VisualNode.NodeKind))!.DeclaringType!.Assembly != vocabulary)
            .Select(type => type.FullName)
            .ToList();

        outside.Should().BeEmpty("ChildSlots reads the kinds Primitives declares, and this test mounts only those");
    }

    [Fact]
    public void EveryNodeKind_HasItsChildrensSlots()
    {
        var nodes = ConcreteNodes();
        nodes.Should().HaveCountGreaterThan(40, "the vocabulary declares its node kinds in Primitives");

        var forgotten = new List<string>();
        var unmounted = new List<string>();
        foreach (var type in nodes)
        {
            if (!TryBuild(type, out var node, out var why))
            {
                unmounted.Add($"{type.Name}: {why}");
                continue;
            }
            try
            {
                var owner = ComponentBoundary.Owner();
                owner.Build(node!, Context, WindowSizeClass.Compact, rebuildAll: true);
                owner.EndFrame();
            }
            catch (Exception failure) when (failure.Message.Contains("knows no child slots", StringComparison.Ordinal)
                || failure is InvalidCastException)
            {
                forgotten.Add($"{type.Name} ({node!.NodeKind}): {failure.Message}");
            }
        }

        forgotten.Should().BeEmpty(
            "every node kind the vocabulary declares needs its arm in ChildSlots, and a kind that shares "
            + "another's NodeKind fails the other's typed read");

        var names = unmounted.Select(line => line.Split(':')[0]).ToHashSet(StringComparer.Ordinal);
        var baseline = File.Exists(BaselinePath)
            ? File.ReadAllLines(BaselinePath).Where(line => line.Length > 0 && !line.StartsWith('#'))
                .Select(line => line.Split(':')[0]).ToHashSet(StringComparer.Ordinal)
            : new HashSet<string>(StringComparer.Ordinal);

        if (Environment.GetEnvironmentVariable("EQ_UPDATE_CHILD_SLOTS_BASELINE") == "1")
        {
            // Only ever takes lines OUT: a kind that now mounts leaves the baseline, and a new one that
            // does not is never written in for you.
            var kept = File.Exists(BaselinePath)
                ? File.ReadAllLines(BaselinePath).Where(line => line.Length == 0 || line.StartsWith('#')
                    || names.Contains(line.Split(':')[0]))
                : [];
            File.WriteAllLines(BaselinePath, kept);
            return;
        }

        names.Except(baseline).Should().BeEmpty(
            "a node kind no instance can be built for is a kind this test cannot hold to its arm. Make it "
            + $"buildable through FactoryArguments; the baseline only shrinks. Not built: {string.Join("; ", unmounted)}");
        baseline.Except(names).Should().BeEmpty(
            "a kind in the baseline mounts now, so it leaves it: regenerate with EQ_UPDATE_CHILD_SLOTS_BASELINE=1");
    }
}
