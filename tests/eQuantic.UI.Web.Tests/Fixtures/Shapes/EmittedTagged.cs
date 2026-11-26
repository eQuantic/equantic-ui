using System;
using System.Collections.Generic;
using eQuantic.UI.Primitives;

namespace eQuantic.UI.Web.Tests.Fixtures;

public sealed record EmittedTagged<T>(T Shape, string Tag) where T : EmittedShapeBase
{
    public int Area => Shape.Area;
}
