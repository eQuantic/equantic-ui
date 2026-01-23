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

public class Table<T> : HtmlElement
{
    public List<T> Data { get; set; } = new();
    public List<TableColumn> Columns { get; set; } = new();
    public bool Striped { get; set; } = true;
    public bool Bordered { get; set; }
    public bool Hoverable { get; set; } = true;
    public Action<T>? OnRowClick { get; set; }

    public override HtmlNode Render()
    {
        var attrs = BuildAttributes();
        var classes = "table";
        if (Striped) classes += " table-striped";
        if (Bordered) classes += " table-bordered";
        if (Hoverable) classes += " table-hover";
        
        var existingClass = attrs.GetValueOrDefault("class");
        attrs["class"] = string.IsNullOrEmpty(existingClass) ? classes : $"{existingClass} {classes}";

        // Header
        var headerCells = Columns.Select(col => new HtmlNode
        {
            Tag = "th",
            Attributes = col.Width != null 
                ? new Dictionary<string, string?> { ["style"] = $"width: {col.Width}" } 
                : new Dictionary<string, string?>(),
            Children = { HtmlNode.Text(col.Header) }
        }).ToList();

        var thead = new HtmlNode
        {
            Tag = "thead",
            Children = {
                new HtmlNode
                {
                    Tag = "tr",
                    Children = headerCells
                }
            }
        };

        // Body
        var rows = Data.Select(item =>
        {
            var cells = Columns.Select(col =>
            {
                var value = GetPropertyValue(item, col.Field);
                var displayValue = col.Formatter != null && value != null
                    ? col.Formatter(value)
                    : value?.ToString() ?? "";

                return new HtmlNode
                {
                    Tag = "td",
                    Children = { HtmlNode.Text(displayValue) }
                };
            }).ToList();

            return new HtmlNode
            {
                Tag = "tr",
                Children = cells
            };
        }).ToList();

        var tbody = new HtmlNode
        {
            Tag = "tbody",
            Children = rows
        };

        return new HtmlNode
        {
            Tag = "table",
            Attributes = attrs,
            Children = { thead, tbody }
        };
    }

    private static object? GetPropertyValue(T item, string propertyName)
    {
        if (item == null) return null;
        var prop = typeof(T).GetProperty(propertyName);
        return prop?.GetValue(item);
    }
}
