using System;
using System.Collections.Generic;
using eQuantic.UI.Primitives;

namespace eQuantic.UI.Web.Tests.Fixtures;

public sealed class EmittedBag : EmittedBase
{
    private List<string?> _items = new();
    public EmittedBag(string name) : base(name) { }
    public int Count => _items.Count;
}
