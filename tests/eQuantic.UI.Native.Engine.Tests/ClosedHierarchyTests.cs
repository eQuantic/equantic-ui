using System.Reflection;
using eQuantic.UI.Primitives;
using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// The vocabulary is CLOSED, and that is what makes <see cref="IVisualNodeVisitor{TState,TResult}"/>
/// worth having.
///
/// <para>
/// An exhaustive visitor is exhaustive only while nothing outside `Primitives` can add a node. Every
/// node that is not a component has a `private protected` constructor now, so a subtype declared
/// anywhere else does not compile — and this asserts the property rather than the spelling, because
/// the spelling has more doors than it looks. `FlexNode` is a PUBLIC ABSTRACT intermediate: closing
/// `VisualNode` alone left it inheriting an accessible constructor, so a second `FlexNode` could
/// still be declared in someone else's assembly.
/// </para>
///
/// <para>
/// It reads every assembly of both graphs rather than `Primitives` alone, and that is the lesson
/// #135 paid for: a duplicate type straddled the seam between two assemblies and a scan of one
/// reported nothing. An intermediate born OUTSIDE the vocabulary is exactly the thing this is for,
/// so looking only inside it would be a pin agreeing with itself.
/// </para>
///
/// <para>
/// No baseline and no exemption list. Three properties, all of them true today, and each stated as
/// what a reader should be able to assume: the only node an app can declare is a component; nothing
/// abstract in the vocabulary can be extended from outside it; nothing concrete in it can be
/// extended at all.
/// </para>
/// </summary>
public class ClosedHierarchyTests
{
    /// <summary>
    /// Every eQuantic assembly this test's own output directory holds that can SEE the vocabulary —
    /// derived, not listed.
    ///
    /// <para>
    /// A hand-written list is a pin that only asks what somebody remembered to write down, and the
    /// first version of this file was one: it named eight assemblies and a guard that compared them
    /// against `AppDomain.CurrentDomain` promptly found two more. That guard was worse than the
    /// list, though — what is LOADED depends on which tests ran, so it passed on a filtered run and
    /// failed on the whole suite, having found five.
    /// </para>
    ///
    /// <para>
    /// The build output is the deterministic answer to the same question: it holds exactly what this
    /// project references, transitively, whatever order anything runs in. Test assemblies are
    /// excluded on purpose — a fixture declaring a fake node is legitimate and is not a node anyone
    /// ships.
    /// </para>
    /// </summary>
    private static readonly Assembly[] Scanned = Discover();

    private static Assembly[] Discover()
    {
        var vocabulary = typeof(VisualNode).Assembly;
        var found = new List<Assembly> { vocabulary };
        foreach (var path in Directory.EnumerateFiles(AppContext.BaseDirectory, "eQuantic.*.dll"))
        {
            var name = Path.GetFileNameWithoutExtension(path);
            if (name.EndsWith(".Tests", StringComparison.Ordinal)) continue;
            if (name == vocabulary.GetName().Name) continue;
            Assembly assembly;
            // A native shell for ANOTHER platform sits in the output and cannot load here; it also
            // cannot declare a node that reaches this machine's tree. Skipped by the loader saying
            // so, rather than by a name this file would have to keep in step.
            try { assembly = Assembly.LoadFrom(path); }
            catch (BadImageFormatException) { continue; }
            catch (FileLoadException) { continue; }
            if (assembly.GetReferencedAssemblies().Any(r => r.Name == vocabulary.GetName().Name))
                found.Add(assembly);
        }
        return [.. found];
    }

    private static IEnumerable<Type> EveryNodeType() => Scanned
        .SelectMany(assembly => assembly.GetTypes())
        .Where(type => typeof(VisualNode).IsAssignableFrom(type) && type != typeof(VisualNode));

    /// <summary>
    /// The only node an app declares is a COMPONENT. Everything else in the vocabulary is the
    /// vocabulary's, which is the sentence the visitor rests on.
    /// </summary>
    [Fact]
    public void ANodeDeclaredOutsideTheVocabulary_IsAComponent()
    {
        var strangers = EveryNodeType()
            .Where(type => type.Assembly != typeof(VisualNode).Assembly)
            .Where(type => !typeof(UiComponent).IsAssignableFrom(type))
            .Select(type => $"{type.FullName} ({type.Assembly.GetName().Name})")
            .ToList();

        strangers.Should().BeEmpty(
            "a node outside Primitives that is not a UiComponent is a node IVisualNodeVisitor "
            + "cannot name, so an exhaustive pass over the vocabulary silently is not one: "
            + string.Join(", ", strangers));
    }

    /// <summary>
    /// Every abstract node in the vocabulary is closed to the outside. `private protected` compiles
    /// to FamANDAssem, which is the one accessibility that says "a subclass, and only in here".
    /// </summary>
    [Fact]
    public void AnAbstractNode_CanOnlyBeExtendedInsideTheVocabulary()
    {
        var seams = new[] { typeof(UiComponent), typeof(StatelessComponent), typeof(StatefulComponent) };

        foreach (var type in EveryNodeType()
                     .Where(t => t.IsAbstract && t.Assembly == typeof(VisualNode).Assembly)
                     .Where(t => !seams.Contains(t)))
        {
            var reachable = type
                .GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                .Where(c => c.IsPublic || c.IsFamily || c.IsFamilyOrAssembly)
                .ToList();

            reachable.Should().BeEmpty(
                $"{type.Name} is a public abstract node, so it INHERITS an accessible constructor "
                + "unless it declares a private protected one — which is how FlexNode stayed open "
                + "while VisualNode was closed, and how a second FlexNode could have been declared "
                + "in any assembly that references the vocabulary");
        }
    }

    /// <summary>…and the concrete ones cannot be extended at all, which the 40 already were. Stated
    /// anyway: an unsealed one is a subtype the visitor would dispatch as its BASE, silently.</summary>
    [Fact]
    public void AConcreteNode_IsSealed()
    {
        var open = EveryNodeType()
            .Where(type => !type.IsAbstract && type.Assembly == typeof(VisualNode).Assembly)
            .Where(type => !type.IsSealed)
            .Select(type => type.Name)
            .ToList();

        open.Should().BeEmpty(
            "a subclass of one of these would be handed to Visit(Base, …) and answered as its base, "
            + "which is worse than not compiling: " + string.Join(", ", open));
    }

    /// <summary>
    /// The guard that keeps the three honest. A pin whose scan came back empty passes by having
    /// nothing to say, and this suite has been caught by that shape before.
    /// </summary>
    [Fact]
    public void TheScanActuallySeesTheVocabulary()
    {
        EveryNodeType().Count(t => !t.IsAbstract && t.Assembly == typeof(VisualNode).Assembly)
            .Should().Be(40, "the vocabulary is 40 concrete nodes, and IVisualNodeVisitor has a "
                + "method for each — if this number moved, the interface moved with it or the "
                + "compiler would have said so");

        EveryNodeType().Should().Contain(typeof(eQuantic.UI.Components.Button),
            "the component library is in the scan, so a node declared there would be seen");

        // A node declared OUTSIDE the vocabulary is what the first assertion exists to catch, and
        // the scan has to be able to see one. Measured rather than assumed: the first A/B for that
        // assertion declared its stranger in this TEST project, the pin stayed green, and the
        // conclusion "it discriminates" would have been wrong. Redone inside eQuantic.UI.Web it
        // failed and named the type — so the names below are the ones that A/B depends on.
        var scanned = Scanned.Select(a => a.GetName().Name).ToList();
        scanned.Should().Contain(
            ["eQuantic.UI.Web", "eQuantic.UI.Email", "eQuantic.UI.Components", "eQuantic.UI.Charts"],
            "both graphs are the point: a node born in a realizer is exactly the case a scan of the "
            + "native side alone would report nothing about");
        scanned.Should().HaveCountGreaterThan(8,
            "the scan is derived from the build output, so it grows with the tree — a number this "
            + "low means the discovery found almost nothing and the three assertions above are "
            + "asking about an empty set");
    }
}
