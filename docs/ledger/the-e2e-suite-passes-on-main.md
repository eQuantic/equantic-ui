- **2026-11-11 · The e2e suite passes on main again**: five of its thirteen cases failed unnoticed, since CI does
  not run it (#286), and each was the stale side ([#1369](https://github.com/eQuantic/equantic-ui/issues/1369)).
  The two passkeys cases staged their outage with a glob of the actions endpoint's path, which the culture
  query the address has carried since #1194 never matched. The two identity cases compared the server markup with
  the page after the density switch #688 brought in, which re-renders a first visit at the pointer's density by
  design; they now let the density settle first. The Link case waited for hint copy #1072 rewrote, and now waits
  for the sheet's grid. Thirteen of thirteen, against the dashboard in Chromium.
