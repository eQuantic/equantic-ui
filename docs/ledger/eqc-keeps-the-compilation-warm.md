- **2026-11-09 · A served eqc keeps the project's compilation between saves**: the eqc the server
  keeps running loaded the project's compilation from nothing on every compile, every reference read
  and bound again. A build now starts from the compilation the one before it loaded and replaces in
  place the trees whose text changed, so the trees keep a cold load's order, and anything a cold load
  would read differently loads cold. A test holds a warm build to what a cold one writes after the same
  edit. Measured on the dashboard sample with the same instrument, which now records the load average
  of each run: eqc's part went from 4.2 to 7.9s (median 5.5s, load 35 to 38) to 3.1 to 5.3s (median
  3.6s, load 27 to 31), and the total from a median of 5.7s to 3.8s
  ([#1278](https://github.com/eQuantic/equantic-ui/issues/1278)). Proposed through OpenSpec
  (`openspec/specs/hot-reload`).
