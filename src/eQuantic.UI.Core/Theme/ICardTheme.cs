using System.Collections.Generic;

namespace eQuantic.UI.Core.Theme;

public interface ICardTheme
{
    string Container { get; }
    string Header { get; }
    string Body { get; }
    string Footer { get; }
    Dictionary<string, string> Shadows { get; }
    string GetShadowInfo(string shadow);
}
