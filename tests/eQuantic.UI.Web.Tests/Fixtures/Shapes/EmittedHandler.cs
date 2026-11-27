using System;
using System.Collections.Generic;
using eQuantic.UI.Primitives;

namespace eQuantic.UI.Web.Tests.Fixtures;

public sealed class EmittedHandler<T> where T : EmittedShapeBase
{
    private readonly Action<T> _use;
    public EmittedHandler(Action<T> use) { _use = use; }
    public void Give(EmittedShapeBase shape) => _use((T)shape);
    public static EmittedShapeBase Unbox(object value) => (T)value;
    public EmittedShapeBase Same(object value)
    {
        static TOut Cast<TOut>(object held) => (TOut)held;
        return Cast<EmittedShapeBase>(value);
    }
}
