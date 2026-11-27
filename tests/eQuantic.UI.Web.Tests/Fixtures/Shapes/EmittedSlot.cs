using System;
using System.Collections.Generic;
using eQuantic.UI.Primitives;

namespace eQuantic.UI.Web.Tests.Fixtures;

public struct EmittedSlot<TValue>
{
    public TValue Value;
    public EmittedSlot(TValue value) { Value = value; }
}
