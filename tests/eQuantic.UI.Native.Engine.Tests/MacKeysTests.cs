using System.Runtime.Versioning;
using eQuantic.UI.Native.Shell.MacOS;
using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// The macOS shell's key names, the DOM's own, so a chord is authored once for every host. A
/// function key types a character in the private use area (F7 types U+F70A), and before the key
/// code named it, that character WAS its name: no chord written <c>F7</c> could match it, and the
/// code diff's F7 did nothing on the Mac. Pure, but the shell's assembly is the Mac's alone, so it
/// runs where the shell does, as the Windows shell's key names do on Windows.
/// </summary>
[SupportedOSPlatform("macos")]
public class MacKeysTests
{
    [MacTheory]
    [InlineData((ushort)36, "\r", "Enter")]
    [InlineData((ushort)48, "\t", "Tab")]
    [InlineData((ushort)53, "\u001B", "Escape")]
    [InlineData((ushort)123, "\uF702", "ArrowLeft")]
    [InlineData((ushort)116, "\uF72C", "PageUp")]
    [InlineData((ushort)121, "\uF72D", "PageDown")]
    [InlineData((ushort)122, "\uF704", "F1")]
    [InlineData((ushort)98, "\uF70A", "F7")]
    [InlineData((ushort)111, "\uF70F", "F12")]
    [InlineData((ushort)90, "\uF717", "F20")]
    [InlineData((ushort)40, "k", "k")]
    [InlineData((ushort)12, "a", "a")]
    public void KeysSpeakTheDomsNames(ushort keyCode, string characters, string expected) =>
        MacKeys.NameOf(keyCode, characters).Should().Be(expected);

    [MacFact]
    public void EveryFunctionKey_IsNamedInOrder_AndIsAFunctionKey()
    {
        ushort[] codes = [122, 120, 99, 118, 96, 97, 98, 100, 101, 109, 103, 111, 105, 107, 113, 106, 64, 79, 80, 90];

        codes.Select(code => MacKeys.NameOf(code, "")).Should().Equal(Enumerable.Range(1, 20).Select(n => "F" + n));
        codes.Should().OnlyContain(code => MacKeys.IsFunctionKey(code));
        MacKeys.IsFunctionKey(40).Should().BeFalse("k types, and goes through the input method");
    }
}
