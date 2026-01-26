using System;
using System.Collections.Generic;
using System.Linq;
using eQuantic.UI.Core;

namespace eQuantic.UI.Components.Display;

public class TableColumn
{
    public string Header { get; set; } = string.Empty;
    public string Field { get; set; } = string.Empty;
    public Func<object, string>? Formatter { get; set; }
    public bool Sortable { get; set; }
    public string? Width { get; set; }
}

public class Table<T> : StatelessComponent
{
    public List<T> Data { get; set; } = new();
    public List<TableColumn> Columns { get; set; } = new();
    public bool Striped { get; set; } = true;
    public bool Bordered { get; set; }
    public bool Hoverable { get; set; } = true;
    public Action<T>? OnRowClick { get; set; }

    public override IComponent Build(RenderContext context)
    {
        var theme = context.GetService<eQuantic.UI.Core.Theme.IAppTheme>();
        var tableTheme = theme?.Table;

        var wrapperClass = tableTheme?.Wrapper ?? "";
        var tableClass = tableTheme?.Table ?? "";
        var headerClass = tableTheme?.Header ?? "";
        var rowClass = tableTheme?.Row ?? "";
        var headCellClass = tableTheme?.HeadCell ?? "";
        var cellClass = tableTheme?.Cell ?? "";

        var attrs = new Dictionary<string, string>
        {
            ["class"] = $"{tableClass} {ClassName}".Trim()
        };

        // Header
        var headerCells = new List<IComponent>();
        foreach(var col in Columns)
        {
            var thAttrs = new Dictionary<string, string> { ["class"] = headCellClass };
            if (col.Width != null) thAttrs["style"] = $"width: {col.Width}";
            
            var th = new DynamicElement 
            { 
                TagName = "th", 
                CustomAttributes = thAttrs
            };
            th.Children.Add(new Text(col.Header));
            headerCells.Add(th);
        }

        var headerRow = new DynamicElement
        {
            TagName = "tr",
            CustomAttributes = new Dictionary<string, string> { ["class"] = "border-b transition-colors hover:bg-muted/50 data-[state=selected]:bg-muted" }
        };
        foreach(var h in headerCells) headerRow.Children.Add(h);

        var thead = new DynamicElement
        {
            TagName = "thead",
            CustomAttributes = new Dictionary<string, string> { ["class"] = headerClass },
            Children = { headerRow }
        };

        // Body
        var rows = new List<IComponent>();
        foreach(var item in Data)
        {
            var cells = new List<IComponent>();
            foreach(var col in Columns)
            {
                var value = GetPropertyValue(item, col.Field);
                var displayValue = col.Formatter != null && value != null
                    ? col.Formatter(value)
                    : value?.ToString() ?? "";

                var cell = new DynamicElement
                {
                    TagName = "td",
                    CustomAttributes = new Dictionary<string, string> { ["class"] = cellClass }
                };
                cell.Children.Add(new Text(displayValue));
                cells.Add(cell);
            }

            var trAttrs = new Dictionary<string, string> { ["class"] = rowClass };
            // Row click handling logic would require binding an event, but DynamicElement OnClick is simple delegate.
            var events = new Dictionary<string, Delegate>();
            if (OnRowClick != null)
            {
                 // We can use a trick to capture 'item' 
                 events["click"] = (Action)(() => OnRowClick(item));
                 trAttrs["class"] += " cursor-pointer";
            }

            var rowElement = new DynamicElement
            {
                TagName = "tr",
                CustomAttributes = trAttrs,
                CustomEvents = events
            };
            foreach(var c in cells) rowElement.Children.Add(c);
            rows.Add(rowElement);
        }

        var tbody = new DynamicElement
        {
            TagName = "tbody"
        };
        foreach(var r in rows) tbody.Children.Add(r);

        var tableElement = new DynamicElement
        {
            TagName = "table",
            CustomAttributes = attrs,
            Children = { thead, tbody }
        };

        // Wrap in div
        return new DynamicElement
        {
            TagName = "div",
            CustomAttributes = new Dictionary<string, string> { ["class"] = wrapperClass },
            Children = { tableElement }
        };
    }

    private static object? GetPropertyValue(T item, string propertyName)
    {
        if (item == null) return null;
        var prop = typeof(T).GetProperty(propertyName);
        return prop?.GetValue(item);
    }
}
