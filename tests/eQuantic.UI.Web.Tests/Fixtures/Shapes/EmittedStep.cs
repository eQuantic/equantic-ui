using System;
using System.Collections.Generic;
using eQuantic.UI.Primitives;

namespace eQuantic.UI.Web.Tests.Fixtures;

public abstract class EmittedStep
{
    public int Run() => Draw() + Measure() + Layout() + Paint();
    public abstract int Draw();
    internal abstract int Measure();
    private protected abstract int Layout();
    protected abstract int Paint();
    public abstract System.Threading.Tasks.Task<int> LoadAsync(int from, int count = 10);
    public abstract System.Threading.Tasks.Task SaveAsync(params int[] values);
    public abstract T Pick<T>(T first, T second);
}
