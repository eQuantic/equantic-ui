using System;
using System.Collections.Generic;
using eQuantic.UI.Primitives;

namespace eQuantic.UI.Web.Tests.Fixtures;

public sealed class EmittedRunner
{
    private readonly Func<int, int> _step;
    public EmittedRunner(Func<int, int> step) : this(step, live: true) { }
    private EmittedRunner(Func<int, int> step, bool live) { _step = step; Live = live; }
    public bool Live { get; }
    public int Run(int value) => _step(value);
}
