using System;
using System.Collections.Generic;
using eQuantic.UI.Primitives;

namespace eQuantic.UI.Web.Tests.Fixtures;

public sealed class EmittedOwner<T> where T : EmittedShapeBase
{
    public EmittedShapeBase Keep(object value) => (T)value;

    public sealed class Held
    {
        public EmittedShapeBase Unwrap(object value) => (T)value;
    }
}
