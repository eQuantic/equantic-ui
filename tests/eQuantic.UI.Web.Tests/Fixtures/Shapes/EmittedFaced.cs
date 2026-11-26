using System;
using System.Collections.Generic;
using eQuantic.UI.Primitives;

namespace eQuantic.UI.Web.Tests.Fixtures;

public sealed class EmittedFaced<T> where T : IEmittedShaped
{
    private readonly T _shape;
    public EmittedFaced(T shape) { _shape = shape; }
    public int Area() => _shape.Area;
    public string Text() => _shape.Describe("cm") + " " + _shape.Label;
}
