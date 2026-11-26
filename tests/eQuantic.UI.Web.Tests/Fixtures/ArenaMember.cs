using eQuantic.UI.Primitives;

namespace eQuantic.UI.Web.Tests.Fixtures;

/// <summary>
/// A gesture of the SDK's as its arena sees it (#1563): a press, a drag, a scroll's pan, a member that is no
/// recognizer and resolves through the entry the router hands it. Transpiled beside <see cref="ArenaProbe"/>.
/// </summary>
public sealed class ArenaMember : IGestureArenaMember
{
    private readonly string _name;
    private readonly List<string> _heard;

    public ArenaMember(string name, List<string> heard)
    {
        _name = name;
        _heard = heard;
    }

    public void AcceptGesture(int pointer) => _heard.Add($"{_name}.accept {pointer}");

    public void RejectGesture(int pointer) => _heard.Add($"{_name}.reject {pointer}");
}
