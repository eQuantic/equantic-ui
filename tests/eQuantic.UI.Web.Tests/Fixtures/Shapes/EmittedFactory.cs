using System;
using System.Collections.Generic;
using eQuantic.UI.Primitives;

namespace eQuantic.UI.Web.Tests.Fixtures;

public sealed class EmittedFactory<T>
{
    private readonly Func<T> _create;
    private readonly Action<T> _initialize;
    public EmittedFactory(Func<T> create, Action<T> initialize) { _create = create; _initialize = initialize; }
    public T Build() { var made = _create(); _initialize(made); return made; }
}
