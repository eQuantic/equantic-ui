using eQuantic.UI.Primitives;

namespace DefaultUIDashboard;

/// <summary>
/// The dashboard's icon, drawn from its own artwork under Assets, as a designer would hand it over:
/// the mark's bead on the mark's plate for a launcher, and the bead alone, with a heavier check, for
/// the tab. The build rasterizes both and the server links them, so no page writes a favicon tag.
/// </summary>
public sealed class AppIcon : IAppIcon
{
    public VisualNode Build(ComponentContext context, AppIconUse use) =>
        Drawing(use == AppIconUse.Small ? Vectors.Bead : Vectors.Launcher, SizeValue.Fill, label: "Dashboard");
}
