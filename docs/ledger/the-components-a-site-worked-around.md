- **2026-11-21 · The components a real site worked around**: five gaps, each met in a browser. A picker
  drawn again without the value it reported says so in development now (`DevelopmentNotices`, written to
  the console by the development runtime and to the logger by a Photon app in Development), and
  `LabelHidden` leaves a field's label out of the drawing for a form that lays its own.
  `Image.FocalPoint` and `Image.Zoom` frame a photograph the way the site's `FramedImage` did, as
  `object-position` and a scale around it on the web and the same arithmetic on Photon. A canvas whose
  handlers follow a drag writes `touch-action: none`, so a finger's drag moves what they move instead of
  scrolling the page, while a chart's hover leaves the scroll to the page. `Navigator.OpenAsync` and
  `DownloadAsync` open what an action answered with from the handler that awaited it, and
  `Link.OpensBeside` opens a destination in a new tab cut from the page
  ([#1780](https://github.com/eQuantic/equantic-ui/issues/1780),
  [#1785](https://github.com/eQuantic/equantic-ui/issues/1785),
  [#1786](https://github.com/eQuantic/equantic-ui/issues/1786),
  [#1787](https://github.com/eQuantic/equantic-ui/issues/1787),
  [#941](https://github.com/eQuantic/equantic-ui/issues/941)). Proposed and archived through OpenSpec
  (`openspec/specs/development-notices`, `image-framing`, `opening-addresses`, `links`, `raw-pointer`,
  `text-entry`).
