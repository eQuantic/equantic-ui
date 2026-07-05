using System;
using eQuantic.UI.Core;

namespace eQuantic.UI.Web.Components.Navigation.Tabs;

public class Tabs : StatelessComponent
{
    public string? DefaultValue { get; set; }
    public string? Value { get; set; }
    public Action<string>? OnValueChange { get; set; }
    public bool Vertical { get; set; }

    public override IComponent Build(RenderContext context)
    {
        var activeValue = Value ?? DefaultValue;
        
        foreach (var child in Children)
        {
            if (child is ITabsComponent tabsComponent)
            {
                tabsComponent.Tabs = this;
                tabsComponent.ActiveValue = activeValue;
            }
            // Deep search for triggers inside TabsList
            else if (child is HtmlElement element)
            {
                AssignTabsToChildren(element, this, activeValue);
            }
        }

        var box = new Box
        {
            ClassName = $"tabs-root flex {(Vertical ? "flex-row" : "flex-col")} {ClassName}".Trim()
        };
        foreach (var child in Children) box.Children.Add(child);
        return box;
    }

    private void AssignTabsToChildren(IComponent parent, Tabs tabs, string? activeValue)
    {
        if (parent is HtmlElement element)
        {
            foreach (var child in element.Children)
            {
                if (child is ITabsComponent tabsComponent)
                {
                    tabsComponent.Tabs = tabs;
                    tabsComponent.ActiveValue = activeValue;
                }
                else if (child is HtmlElement subElement)
                {
                    AssignTabsToChildren(subElement, tabs, activeValue);
                }
            }
        }
    }
}

public interface ITabsComponent : IComponent
{
    Tabs? Tabs { get; set; }
    string? ActiveValue { get; set; }
}

public abstract class TabsSubComponent : StatelessComponent, ITabsComponent
{
    public Tabs? Tabs { get; set; }
    public string? ActiveValue { get; set; }
}
