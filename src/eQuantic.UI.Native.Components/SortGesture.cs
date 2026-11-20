using eQuantic.UI.Primitives;

namespace eQuantic.UI.Native.Components;

/// <summary>
/// An item the host is carrying, or may carry, between the lists of a group (#1537): the Photon half of what
/// the browser's <c>dom/sortable.ts</c> does, with the same arithmetic (<see cref="SortPlacement"/>).
/// <para>
/// A pointer's candidate starts at the press, over an item, and is the host's to ARM: a mouse or a pen past
/// <see cref="SortPlacement.PickUpTravel"/>, a finger after holding still for <see cref="SortPlacement.HoldMs"/>.
/// A keyboard's starts armed, from the focused item, and moves a place at a time.
/// </para>
/// </summary>
internal sealed class SortGesture(Element item, SortableItem itemNode, Element source, Sortable sourceNode)
{
    /// <summary>The item carried, and its node.</summary>
    public Element Item { get; } = item;
    public SortableItem ItemNode { get; } = itemNode;

    /// <summary>The list it comes from, and its node.</summary>
    public Element Source { get; } = source;
    public Sortable SourceNode { get; } = sourceNode;

    /// <summary>What pressed it, and where, in the host's points. A keyboard's carry has no press.</summary>
    public PointerKind Kind { get; init; }
    public Point Start { get; init; }

    /// <summary>Whether the keyboard carries it rather than a pointer.</summary>
    public bool ByKeyboard { get; init; }

    /// <summary>What a reader hears the item called, in each step a keyboard's carry says (#1564).</summary>
    public string Name { get; init; } = "";

    /// <summary>The frame time a finger's press was first seen at, which its hold is counted from: a press
    /// carries no time of its own, so the frame it asks for stamps it.</summary>
    public float? PressedAtMs { get; set; }

    /// <summary>Whether it was picked up: a candidate that never arms leaves the press to press.</summary>
    public bool Active { get; set; }

    /// <summary>The lift its position keeps, once picked up.</summary>
    public SortLiftState? Lift { get; set; }

    /// <summary>Where the pointer is now.</summary>
    public Point Pointer { get; set; }

    /// <summary>Where the pointer was across, and the frame time, when the lean was last measured.</summary>
    public float TiltFromX { get; set; }
    public float TiltFromMs { get; set; }

    /// <summary>The frame time the pointer last moved at: a copy left still that long stands up again.</summary>
    public float MovedAtMs { get; set; }

    /// <summary>The lean the pointer's speed asks for, and the lean drawn, which eases toward it.</summary>
    public float Lean { get; set; }
    public float Angle { get; set; }

    /// <summary>The frame time the copy's lean and the scroll under it last advanced at.</summary>
    public float? TickedAtMs { get; set; }

    /// <summary>Where it would land now: the list, its node and the drop that list would report.</summary>
    public (Element List, Sortable Node, SortDrop Drop)? Landing { get; set; }

    /// <summary>The last spot a pointer offered a list's guard, with the depth it asked for (#1594), and the drop the
    /// guard took there, or null when it refused them all: a guard is asked once per place and depth asked for.</summary>
    public (Element List, int Index, int Into, int Wants, SortDrop? Drop)? Asked { get; set; }

    /// <summary>The list a keyboard's carry is in, and the place in it.</summary>
    public (Element List, Sortable Node)? KeyList { get; set; }
    public int KeyIndex { get; set; }

    /// <summary>The item of that list a keyboard's carry is inside, by its place among the others, or -1.</summary>
    public int KeyInto { get; set; } = -1;

    /// <summary>In a list whose items have depths (#1594), the depth a keyboard's place holds, and the one its cross
    /// arrows last asked for, which the item keeps as it moves along, as a pointer keeps its travel across.</summary>
    public int? KeyDepth { get; set; }
    public int KeyWants { get; set; }
}
