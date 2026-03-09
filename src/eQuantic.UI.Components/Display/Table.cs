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
        var theme = context.GetService<Core.Theme.IAppTheme>();
        var tableTheme = theme?.Table;

        var wrapperClass = tableTheme?.Wrapper ?? "eq-table-wrapper";
        var tableClass = tableTheme?.Table ?? "eq-table";
        var headerClass = tableTheme?.Header ?? "eq-table-header";
        var rowClass = tableTheme?.Row ?? "eq-table-row";
        var headCellClass = tableTheme?.HeadCell ?? "eq-table-head-cell";
        var cellClass = tableTheme?.Cell ?? "eq-table-cell";

        var attrs = new Dictionary<string, string>
        {
            ["class"] = $"{tableClass} {ClassName}".Trim()
        };

        // Header
        var headerCells = new List<IComponent>();
        foreach(var col in Columns)
        {
            var th = new Box 
            { 
                As = "th", 
                ClassName = headCellClass,
                Style = col.Width != null ? new HtmlStyle { Width = col.Width } : null
            };
            th.Children.Add(new Text(col.Header));
            headerCells.Add(th);
        }

        var headerRow = new Box
        {
            As = "tr",
            ClassName = "border-b transition-colors hover:bg-muted/50 data-[state=selected]:bg-muted"
        };
        foreach(var h in headerCells) headerRow.Children.Add(h);

        var thead = new Box
        {
            As = "thead",
            ClassName = headerClass,
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

                var cell = new Box
                {
                    As = "td",
                    ClassName = cellClass
                };
                cell.Children.Add(new Text(displayValue));
                cells.Add(cell);
            }

            var trClass = rowClass;
            var events = new Dictionary<string, Delegate>();
            if (OnRowClick != null)
            {
                 events["click"] = (Action)(() => OnRowClick(item));
                 trClass += " cursor-pointer";
            }

            var rowElement = new Box
            {
                As = "tr",
                ClassName = trClass,
                CustomEvents = events
            };
            foreach(var c in cells) rowElement.Children.Add(c);
            rows.Add(rowElement);
        }

        var tbody = new Box
        {
            As = "tbody"
        };
        foreach(var r in rows) tbody.Children.Add(r);

        var tableElement = new Box
        {
            As = "table",
            ClassName = $"{tableClass} {ClassName}".Trim(),
            Children = { thead, tbody }
        };

        // Wrap in div
        return new Box
        {
            As = "div",
            ClassName = wrapperClass,
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
