namespace eQuantic.UI.Core.Theme;

public interface IAppTheme
{
    ICardTheme Card { get; }
    IButtonTheme Button { get; }
    IInputTheme Input { get; }
}

public interface ICardTheme
{
    string Container { get; }
    string Header { get; }
    string Body { get; }
    string Footer { get; }
    string GetShadowInfo(string shadow);
}

public interface IButtonTheme
{
    string Base { get; }
    string GetVariant(string variant);
}

public interface IInputTheme
{
    string Base { get; }
}
