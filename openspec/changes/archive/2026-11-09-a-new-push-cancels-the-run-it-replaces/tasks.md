## 1. A superseded run is cancelled

- [x] 1.1 A `concurrency` group keyed on the pull request's number, cancelling in progress only for `pull_request`
- [x] 1.2 A group of its own run id for every other event, so main, tags, merge groups and runs by hand are never cancelled
- [x] 1.3 Proved on the pull request itself: two pushes a minute apart leave the first run cancelled and the second running
