using System;
using System.Collections.Generic;
using eQuantic.UI.Primitives;

namespace eQuantic.UI.Web.Tests.Fixtures;

public class EmittedBase(string name)
{
    public string Name { get; } = name;
}
