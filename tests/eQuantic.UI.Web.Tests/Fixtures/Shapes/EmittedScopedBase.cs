using System;
using System.Collections.Generic;
using eQuantic.UI.Primitives;

namespace eQuantic.UI.Web.Tests.Fixtures;

public abstract class EmittedScopedBase
{
    protected EmittedScopedBase(int count) { Count = count; }
    public int Count { get; }
}
