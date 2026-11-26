using System;
using System.Collections.Generic;
using eQuantic.UI.Primitives;

namespace eQuantic.UI.Web.Tests.Fixtures;

public sealed class EmittedItem(int depth)
{
    public int Depth { get; } = depth;
}
