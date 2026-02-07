using System;
using System.Collections.Generic;
using eQuantic.UI.Core;

namespace eQuantic.UI.Components.Display.Accordion;

public class AccordionItem : AccordionSubComponent
{
    public string Value { get; set; } = string.Empty;

    public override IComponent Build(RenderContext context)
    {
        // Propagate state to children (Trigger and Content)
        foreach (var child in Children)
        {
            if (child is IAccordionItemComponent itemComponent)
            {
                itemComponent.ItemValue = Value;
                itemComponent.Accordion = Accordion;
                itemComponent.ActiveValues = ActiveValues;
            }
            else if (child is HtmlElement element)
            {
                AssignItemToChildren(element);
            }
        }

        var box = new Box
        {
            ClassName = $"border-b border-zinc-200 dark:border-zinc-800 {ClassName}".Trim()
        };
        foreach (var child in Children) box.Children.Add(child);
        return box;
    }

    private void AssignItemToChildren(HtmlElement parent)
    {
        foreach (var child in parent.Children)
        {
            if (child is IAccordionItemComponent itemComponent)
            {
                itemComponent.ItemValue = Value;
                itemComponent.Accordion = Accordion;
                itemComponent.ActiveValues = ActiveValues;
            }
            else if (child is HtmlElement subElement)
            {
                AssignItemToChildren(subElement);
            }
        }
    }
}

public interface IAccordionItemComponent : IAccordionComponent
{
    string? ItemValue { get; set; }
}

public class AccordionTrigger : AccordionSubComponent, IAccordionItemComponent
{
    public string? ItemValue { get; set; }
    public string Text { get; set; } = string.Empty;

    public override IComponent Build(RenderContext context)
    {
        var isOpen = ActiveValues?.Contains(ItemValue ?? "") ?? false;

        var trigger = new DynamicElement
        {
            TagName = "button",
            ClassName = $"flex flex-1 items-center justify-between py-4 font-medium transition-all hover:underline data-[state=open]:rotate-0 {ClassName}".Trim(),
            CustomAttributes = new Dictionary<string, string>
            {
                ["type"] = "button",
                ["data-state"] = isOpen ? "open" : "closed"
            },
            OnClick = () => {
                if (Accordion == null || string.IsNullOrEmpty(ItemValue)) return;
                
                var newValues = new List<string>(ActiveValues ?? new List<string>());
                if (isOpen)
                {
                    if (Accordion.Collapsible) newValues.Remove(ItemValue);
                }
                else
                {
                    if (Accordion.Type == AccordionType.Single) newValues.Clear();
                    newValues.Add(ItemValue);
                }
                Accordion.OnValueChange?.Invoke(newValues);
            }
        };

        trigger.Children.Add(new Text(Text));
        trigger.Children.Add(new Box {
            ClassName = "h-4 w-4 shrink-0 transition-transform duration-200",
            DataAttributes = new Dictionary<string, string> { ["state"] = isOpen ? "open" : "closed" },
            Children = { new Text("▼") }
        });
        foreach (var child in Children) trigger.Children.Add(child);

        return trigger;
    }
}

public class AccordionContent : AccordionSubComponent, IAccordionItemComponent
{
    public string? ItemValue { get; set; }

    public override IComponent Build(RenderContext context)
    {
        var isOpen = ActiveValues?.Contains(ItemValue ?? "") ?? false;
        if (!isOpen) return new NullComponent();

        var box = new Box
        {
            ClassName = $"overflow-hidden text-sm transition-all animate-in slide-in-from-top-2 fade-in duration-200 {ClassName}".Trim()
        };
        var inner = new Box { ClassName = "pb-4 pt-0" };
        foreach (var child in Children) inner.Children.Add(child);
        box.Children.Add(inner);
        return box;
    }
}
