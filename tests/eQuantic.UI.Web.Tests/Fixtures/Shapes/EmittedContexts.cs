using System;
using System.Collections.Generic;
using eQuantic.UI.Primitives;

namespace eQuantic.UI.Web.Tests.Fixtures;

// A context annotated as the runtime's interface and constructed as its class, as the element engine builds
// one per position (#1475).
public sealed class EmittedContexts
{
    private ComponentContext? _context;

    public ComponentContext For(IAppTheme theme) => _context ??= new ComponentContext(theme);
}
