- **2026-11-09 · A modal layer holds the Tab walk on Photon**: a bug under #849
  ([#1066](https://github.com/eQuantic/equantic-ui/issues/1066)). On Photon a dialog, a sheet or a drawer did not
  hold the keyboard: Tab walked every focus stop of the frame in paint order, so from a dialog's last control it
  landed on the page's button behind the scrim, where the web's focus trap cycles to the dialog's first. The
  realizer's result now names the root of the topmost modal and open layer, and the host's focus walk visits only
  the stops built under it and wraps among them: a menu opened in the dialog is part of the walk, a toast is not,
  and a dialog with no control holds the keyboard with nothing behind it focused. The control that has the focus
  still answers Tab first, and the `modal-focus` scenarios now run on Photon too. Proved with tests that failed
  first. Proposed and archived through OpenSpec (`modal-focus`).
