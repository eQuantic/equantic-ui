- **2026-11-09 · An app declares its icon once and the SDK writes every favicon**: the site and
  myartwork3d wrote their favicon links by hand, because the icon the SDK already rasterized could not
  draw the app's own artwork (eqicon compiled `AppIcon.cs` alone, so `Box(...)` was the vocabulary's
  type and `Vectors` did not exist), drew every vector as a placeholder box where no platform
  rasterizer exists, Linux included, had one artwork where a brand has two, and answered `/favicon.ico`
  with a 404. `IAppIcon.Build` takes the `AppIconUse` it is drawn for, the icon compiles with
  the app's usings and its vector catalog, a rasterizer in plain C# draws its vectors on every machine,
  and the web set writes `favicon.ico` from the small cut with its transparency. The server answers
  `/favicon.ico` from the app's own file first, a referenced project's static web asset included, then
  from the built one, and every page links it, so an app that wrote that link by hand drops its
  `AddHeadTag`. A C# icon also builds on Windows now: eqicon referenced every `.dll` of the runtime's
  folder, its native libraries included, and each was CS0009 there. The dashboard sample draws its icon from two
  SVGs ([#1240](https://github.com/eQuantic/equantic-ui/issues/1240)). Proposed and archived through
  OpenSpec (`openspec/specs/app-icon`, a new spec).
