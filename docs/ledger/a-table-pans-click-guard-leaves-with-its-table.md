- **2026-11-24 · A table pan's click guard leaves with its click or its table, and the runtime suite's teardown flake
  is gone**: a pan on a table viewport armed a capturing click guard on the window and removed it 400 ms later on a
  bare timer nothing cancelled. A spec torn down within those 400 ms had the timer fire against a window that had lost
  its methods (`target.removeEventListener is not a function`), failing runs in which every file had passed; in an app
  the guard outlived an unmounted table. The guard is the table's now: it goes, timer and listener, with the click it
  stops, and at once when the table stops being watched, which every table is now, observed or not, and the runtime's
  spec setup lets go of the tables each file leaves ([#1876](https://github.com/eQuantic/equantic-ui/issues/1876)).
  Proposed and archived through OpenSpec (`openspec/specs/table-viewport`).
