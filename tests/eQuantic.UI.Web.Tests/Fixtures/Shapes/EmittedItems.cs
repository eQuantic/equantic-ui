using System;
using System.Collections.Generic;
using eQuantic.UI.Primitives;

namespace eQuantic.UI.Web.Tests.Fixtures;

public static class EmittedItems
{
    private static readonly Comparison<EmittedItem> ByDepth = static (a, b) => a.Depth.CompareTo(b.Depth);
    public static void Order(List<EmittedItem> items) => items.Sort(ByDepth);
}
