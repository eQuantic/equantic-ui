using System;
using System.Collections.Generic;
using eQuantic.UI.Primitives;

namespace eQuantic.UI.Web.Tests.Fixtures;

public abstract class EmittedFlex : EmittedShape
{
    public List<EmittedShape> Children { get; } = new();
}
