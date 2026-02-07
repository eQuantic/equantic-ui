using eQuantic.UI.Core.Theme;

namespace eQuantic.UI.Material.Theme;

/// <summary>
/// Material Design 3 Dialog theme implementation
/// </summary>
public class MaterialDialogTheme : IDialogTheme
{
    public string Overlay => "md-dialog-overlay";
    public string Content => "md-dialog";
    public string Header => "md-dialog__header";
    public string Title => "md-dialog__title";
    public string Description => "md-dialog__content";
    public string Footer => "md-dialog__actions";
}
