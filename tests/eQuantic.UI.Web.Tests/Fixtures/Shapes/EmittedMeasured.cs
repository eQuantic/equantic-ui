using System;
using System.Collections.Generic;
using eQuantic.UI.Primitives;

namespace eQuantic.UI.Web.Tests.Fixtures;

public sealed class EmittedMeasured<T> where T : EmittedShapeBase
{
    private readonly T _shape;
    public EmittedMeasured(T shape) { _shape = shape; }
    public int Area() => _shape.Area;
}
