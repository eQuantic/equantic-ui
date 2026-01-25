using System.Collections.Generic;

namespace eQuantic.UI.Core.Theme;

public interface IAppTheme
{
    ICardTheme Card { get; }
    IButtonTheme Button { get; }
    IInputTheme Input { get; }
    ICheckboxTheme Checkbox { get; }
    ITextTheme Typography { get; }
}

public interface ICardTheme
{
    string Container { get; }
    string Header { get; }
    string Body { get; }
    string Footer { get; }
    Dictionary<string, string> Shadows { get; }
    string GetShadowInfo(string shadow);
}

public interface IButtonTheme
{
    string Base { get; }
    Dictionary<string, string> Variants { get; }
    string GetVariant(string variant);
}

public interface IInputTheme
{
    string Base { get; }
}

public interface ICheckboxTheme
{
    string Base { get; }
    string Checked { get; }
    string Unchecked { get; }
}

public interface ITextTheme
{
    string Base { get; }
    Dictionary<string, string> Variants { get; }
    string GetVariant(string variant);
}
