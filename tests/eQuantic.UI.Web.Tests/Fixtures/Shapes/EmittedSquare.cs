using System;
using System.Collections.Generic;
using eQuantic.UI.Primitives;

namespace eQuantic.UI.Web.Tests.Fixtures;

public sealed class EmittedSquare(int side) : EmittedShape
{
    public int Side { get; } = side;
}
