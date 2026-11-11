- **2026-11-09 · One component announces alike on every target, and the tests say where it does not**:
  `SemanticsParityTests` reads every public component beside the native suite (built with plausible
  arguments), every `PressableRole`, and curated cases with real content and opened disclosures, on the
  web through the component tester and on Photon through the host's semantics tree, and compares role,
  name, value, the five states and order. Its first run found 165 differences: 95 were the tester's own
  reading of the web, which now follows `aria-owns`, names a field by its title and placeholder, and reads
  a live region as the vocabulary's group; the rest are six families of real differences, filed as #1328
  to #1333 and written into a baseline that may only shrink, each entry citing its bug. It replaces
  `AnnouncementParityTests`, whose cases it carries
  ([#339](https://github.com/eQuantic/equantic-ui/issues/339)).
