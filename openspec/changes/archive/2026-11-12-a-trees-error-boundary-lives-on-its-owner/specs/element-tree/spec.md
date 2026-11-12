## ADDED Requirements

### Requirement: A tree's error boundary is its own

Each tree SHALL build through an error boundary of its own, and its owner SHALL be made from it. The
boundary's diagnostics, its report and its tally of contained components SHALL be the tree's, never an
async flow's: the server SHALL keep one for a request and hand it to the request's host and to each
round's channels, a Photon host SHALL keep one for its window, and each shell SHALL arm its window's
with the app's report and read its tally beside its frame summary.

#### Scenario: Two windows in one process

- **WHEN** a component throws in one window's tree and the other window's tree builds without a failure
- **THEN** the first window's tally names it and its report hears it, and the second window's tally is
  empty and its report hears nothing

#### Scenario: A failure inside an escape-hatch page

- **WHEN** a write-once bridge inside an escape-hatch page contains a failure while the server renders it
- **THEN** it is reported through the request's boundary, as a failure in a write-once page is
