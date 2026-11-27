using System;
using System.Collections.Generic;
using eQuantic.UI.Primitives;

namespace eQuantic.UI.Web.Tests.Fixtures;

public abstract class EmittedFramed : EmittedShape
{
    public EmittedShape? Child { get; init; }
}
