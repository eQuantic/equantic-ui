using DefaultUIDashboard.Resources;
using eQuantic.UI.Components;
using eQuantic.UI.Core;
using eQuantic.UI.Primitives;

namespace DefaultUIDashboard.Screens;

/// <summary>
/// Track L M0, live: the strings on this page come from <c>Resources/Strings.resx</c> (+ pt-BR),
/// written exactly as any .NET app writes them. The server resolves them through the real
/// ResourceManager under the request culture; the browser resolves the SAME keys through the
/// culture catalog the build emitted — request it with <c>Accept-Language: pt-BR</c> and both
/// halves answer in Portuguese, byte-identical through hydration.
/// </summary>
[Page("/i18n", Title = "Localization — eQuantic Console")]
public sealed class I18nScreen : StatefulComponent
{
    private string _name = "Ana";

    public override VisualNode Build(ComponentContext context)
    {
        var theme = context.Theme;

        var column = new Column(gap: Space.S4) { Width = SizeValue.Fill };
        column.Add(new Text(Strings.Hero_Title, TypeRole.Heading));
        column.Add(new Text(Strings.Hero_Body, TypeRole.BodyM, theme.TextSecondary));
        column.Add(new TextInput(_name, value => SetState(() => _name = value),
            label: "Name", size: SizeVariant.Medium));
        column.Add(new Text(string.Format(Strings.Greeting, _name), TypeRole.Title));

        return new Box(new BoxStyle { Padding = EdgeInsets.All(Space.S6) }, column);
    }
}
