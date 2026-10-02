using System.Runtime.Versioning;
using eQuantic.UI.Native.Shell.MacOS;
using eQuantic.UI.Primitives;
using FluentAssertions;
using Xunit;
using static eQuantic.UI.Native.Shell.Apple.ObjC;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// What AppKit itself is handed, read back from the elements through the real dispatch — the
/// question <see cref="NativeRoleTests"/> cannot ask, since the table is only what the bridge is
/// TOLD (#338).
/// <para>
/// A row in <see cref="eQuantic.UI.Native.Components.NativeRole"/> is a claim until an element
/// carries it, and a tab is where the two can part: AppKit names it with two words, a radio button
/// whose subrole is AXTabButton, and a bridge that set the role and dropped the subrole would
/// announce every tab as a radio button with the table still right. So the elements are built the
/// way <c>accessibilityChildren</c> builds them for VoiceOver, and each one is asked what it is.
/// </para>
/// </summary>
[SupportedOSPlatform("macos")]
public class AppKitAccessibilityTests
{
    [MacFact]
    public void EachPressableRoleReachesAppKitInItsOwnWords()
    {
        SemanticNode NodeOf(SemanticRole role, string name, int index) =>
            new(role, $"p{index}", new Rect(0, index * 30, 120, 24), name, null, false);
        IReadOnlyList<SemanticNode> tree =
        [
            // In the states the walk hands them: a chosen radio's check, a picked tab's selection.
            NodeOf(SemanticRole.Radio, "Express", 0) with { Checked = SemanticCheck.On },
            NodeOf(SemanticRole.Tab, "Overview", 1) with { Selected = true },
            NodeOf(SemanticRole.MenuItem, "Rename", 2),
            NodeOf(SemanticRole.Option, "Lisbon", 3),
            NodeOf(SemanticRole.Destination, "Inbox", 4),
            NodeOf(SemanticRole.Button, "Save", 5),
        ];

        var previous = PhotonAccessibility.Source;
        PhotonAccessibility.Source = () => tree;
        try
        {
            var view = Send(Send(AppKit.Class("NSView"), Sel("alloc")), Sel("init"));
            var children = PhotonAccessibility.BuildChildren(view);

            var said = new Dictionary<string, (string? Role, string? Subrole)>();
            var elements = new Dictionary<string, IntPtr>();
            var count = SendULong(children, Sel("count"));
            for (nuint index = 0; index < count; index++)
            {
                var element = Send(children, Sel("objectAtIndex:"), index);
                var name = FromNSString(Send(element, Sel("accessibilityLabel")))!;
                elements[name] = element;
                said[name] = (
                    FromNSString(Send(element, Sel("accessibilityRole"))),
                    FromNSString(Send(element, Sel("accessibilitySubrole"))));
            }

            said.Should().HaveCount(tree.Count, "every node is one element VoiceOver can land on");
            said["Express"].Should().Be(("AXRadioButton", null));
            said["Overview"].Should().Be(("AXRadioButton", "AXTabButton"),
                "a tab is a radio button until its subrole says otherwise, and the subrole is what VoiceOver reads as \"tab\"");
            said["Rename"].Should().Be(("AXMenuItem", null));
            said["Lisbon"].Should().Be(("AXMenuItem", null));
            said["Inbox"].Should().Be(("AXButton", null), "a destination is named as a button, where the user is rides AXSelected");
            said["Save"].Should().Be(("AXButton", null),
                "the control: a role with no subrole in the table gets none from the bridge either");

            // And the states the roles are read with. A radio's check is its AXValue, the 0/1 a
            // checkbox carries too, and a tab's selection is AXSelected, Core-AAM's word for both.
            SendLong(Send(elements["Express"], Sel("accessibilityValue")), Sel("longValue")).Should().Be(1,
                "a chosen radio says so through AXValue, which VoiceOver reads as its state");
            SendBool(elements["Overview"], Sel("isAccessibilitySelected")).Should().BeTrue(
                "a picked tab says so through AXSelected");
        }
        finally
        {
            PhotonAccessibility.Source = previous;
        }
    }
}
