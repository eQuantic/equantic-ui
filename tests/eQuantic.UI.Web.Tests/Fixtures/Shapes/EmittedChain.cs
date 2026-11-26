using System;
using System.Collections.Generic;
using eQuantic.UI.Primitives;

namespace eQuantic.UI.Web.Tests.Fixtures;

// A for head that declares its variable wider than its initializer: `this`, walked up to null (#1483).
public sealed class EmittedChain
{
    public EmittedChain? Parent { get; init; }

    public int Depth()
    {
        var depth = 0;
        for (EmittedChain? at = this; at != null; at = at.Parent) depth++;
        return depth;
    }
}
