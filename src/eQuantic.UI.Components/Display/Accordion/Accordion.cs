using System;
using System.Collections.Generic;
using eQuantic.UI.Core;

namespace eQuantic.UI.Components.Display.Accordion;

public enum AccordionType { Single, Multiple }

public class Accordion : StatelessComponent
{
    public AccordionType Type { get; set; } = AccordionType.Single;
    public List<string> DefaultValue { get; set; } = new();
    public List<string> Value { get; set; } = new();
    public Action<List<string>>? OnValueChange { get; set; }
    public bool Collapsible { get; set; } = true;

    public override IComponent Build(RenderContext context)
    {
        var activeValues = Value.Count > 0 ? Value : DefaultValue;
        
        AssignAccordionToChildren(this, activeValues);

        var box = new Box
        {
            ClassName = $"accordion-root w-full {ClassName}".Trim()
        };
        foreach (var child in Children) box.Children.Add(child);
        return box;
    }

    private void AssignAccordionToChildren(HtmlElement parent, List<string> activeValues)
    {
        foreach (var child in parent.Children)
        {
            if (child is IAccordionComponent accordionComponent)
            {
                accordionComponent.Accordion = this;
                accordionComponent.ActiveValues = activeValues;
            }
            if (child is HtmlElement element)
            {
                AssignAccordionToChildren(element, activeValues);
            }
        }
    }
}

public interface IAccordionComponent : IComponent
{
    Accordion? Accordion { get; set; }
    List<string>? ActiveValues { get; set; }
}

public abstract class AccordionSubComponent : StatelessComponent, IAccordionComponent
{
    public Accordion? Accordion { get; set; }
    public List<string>? ActiveValues { get; set; }
}
