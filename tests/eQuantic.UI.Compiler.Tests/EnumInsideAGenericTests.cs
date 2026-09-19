using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Compiler.Tests;

/// <summary>
/// What a VOCABULARY ENUM becomes when it rides inside a generic the string mapper already
/// reshaped — `Action&lt;NavigableMove&gt;` is `(navigableMove: …) => void` by the time the symbol
/// pass sees it, and the name inside is the part that has to be rewritten.
///
/// <para>
/// The runtime mirrors a vocabulary enum as the <c>&lt;Enum&gt;Value</c> string union, or as
/// <c>number</c> when it is [Flags], and exports no <c>NavigableMove</c> at all. Emitting the C#
/// spelling therefore names nothing:
/// </para>
///
/// <code>
/// src/shared/components/UI.ts(152,46): error TS2552: Cannot find name 'NavigableMove'.
/// Did you mean 'Navigable'?
/// </code>
///
/// <para>
/// It went unnoticed because a BARE enum parameter has always been right — that goes down the
/// symbol switch, which is asserted here beside the generic one so the two cannot drift. Nothing
/// had put an enum INSIDE a generic: every factory taking a delegate took one over a primitive
/// until <c>UI.Navigable</c> took one over <see cref="Primitives.NavigableMove"/> (#251).
/// </para>
///
/// <para>
/// The whole compiler suite passed with the fix reverted — measured, 906 of 906 — because the only
/// cover was the transpiled pin in the Web tests, and that holds only while the shared component
/// library happens to contain the construct. This is the focused one.
/// </para>
/// </summary>
public class EnumInsideAGenericTests
{
    private static string Emit(string signature) =>
        TestHelper.ConvertClass($"    public static void Probe({signature}) {{ }}");

    [Fact]
    public void AVocabularyEnum_InsideAGeneric_CrossesAsItsUnion()
    {
        var js = Emit("Action<eQuantic.UI.Primitives.NavigableMove> onMove");

        js.Should().Contain("(navigableMove: NavigableMoveValue) => void",
            "the runtime exports the union, and no type called NavigableMove");
        js.Should().NotContain(": NavigableMove)",
            "the C# spelling names nothing on the other side");
    }

    [Fact]
    public void TheUnion_TravelsAsAnImport()
    {
        // An annotation naming something the module never imported is a BROKEN type, not a missing
        // one — so the rewrite has to register the name, not just spell it.
        Emit("Action<eQuantic.UI.Primitives.NavigableMove> onMove")
            .Should().Contain("import { NavigableMoveValue } from \"@equantic/runtime\";");
    }

    [Fact]
    public void AFlagsEnum_InsideAGeneric_CrossesAsNumber()
    {
        // A [Flags] enum's members COMBINE, so its runtime value IS a number — the same answer the
        // symbol switch gives a bare one, and the reason this is not simply "always the union".
        Emit("Action<eQuantic.UI.Primitives.WebSandbox> onGrant")
            .Should().Contain("(webSandbox: number) => void");
    }

    /// <summary>The control: a BARE enum parameter, which has been right all along. It is asserted
    /// here so that a change to either path has to face both answers at once.</summary>
    [Fact]
    public void ABareVocabularyEnum_WasAlwaysRight()
    {
        Emit("eQuantic.UI.Primitives.NavigableMove move")
            .Should().Contain("_move: NavigableMoveValue");
    }

    /// <summary>
    /// The arm the enum one was folded into rather than added beside: an INTERFACE inside a generic
    /// answers the same <c>any</c> a bare interface parameter does, because the runtime can export
    /// no value for one. Generalizing the rewrite must not have cost this.
    /// </summary>
    [Fact]
    public void AnInterface_InsideAGeneric_StillAnswersAny()
    {
        Emit("Action<eQuantic.UI.Primitives.ICanvasPainter> draw")
            .Should().Contain("(iCanvasPainter: any) => void");
    }

    /// <summary>
    /// ONE NESTING DOWN, which the first pass at this did not reach: the string mapper flattens
    /// <c>IReadOnlyList&lt;NavigableMove&gt;</c> to <c>NavigableMove[]</c> before the symbol pass
    /// runs, and walking only the outer generic's own arguments never sees the enum. It emitted the
    /// C# spelling with no import — the same defect as the un-nested case, one level in.
    /// </summary>
    [Fact]
    public void AVocabularyEnum_NestedTwoDeep_CrossesAsItsUnionToo()
    {
        var js = Emit("Action<IReadOnlyList<eQuantic.UI.Primitives.NavigableMove>> onMoves");

        js.Should().Contain("NavigableMoveValue[]");
        js.Should().NotContain("NavigableMove[]", "the C# spelling names nothing on the other side");
        js.Should().Contain("import { NavigableMoveValue } from \"@equantic/runtime\";");
    }

    /// <summary>
    /// An interface nested the same way, for the same reason the un-nested one is asserted: the two
    /// kinds share the walk, so a change to it has to face both.
    /// </summary>
    [Fact]
    public void AnInterface_NestedTwoDeep_AnswersAnyToo()
    {
        Emit("Action<IReadOnlyList<eQuantic.UI.Primitives.ICanvasPainter>> draws")
            .Should().Contain("any[]");
    }

    /// <summary>
    /// A PARAMETER NAME has to be a name. <c>Action&lt;T&gt;</c> derived one from its type argument,
    /// which is fine while the argument IS an identifier and produced <c>iReadOnlyList&lt;T&gt;</c>
    /// in the parameter position when it was not — a module that does not parse at all, found by
    /// the same review that found the nesting. <c>Func&lt;…&gt;</c> beside it has always answered
    /// <c>value</c> without deriving anything, and this now agrees with it.
    /// </summary>
    [Fact]
    public void AGenericTypeArgument_DoesNotBecomeAParameterName()
    {
        var js = Emit("Action<IReadOnlyList<eQuantic.UI.Primitives.NavigableMove>> onMoves");

        js.Should().Contain("(value: NavigableMoveValue[]) => void");
        js.Should().NotContain("iReadOnlyList<", "that is a type, and it was landing in the name slot");
    }

    /// <summary>The control for the one above: a SIMPLE type argument still names the parameter
    /// after itself, which is what every delegate in the tree already emits.</summary>
    [Fact]
    public void ASimpleTypeArgument_StillNamesTheParameter()
    {
        Emit("Action<eQuantic.UI.Primitives.NavigableMove> onMove")
            .Should().Contain("(navigableMove: NavigableMoveValue) => void");
    }
}
