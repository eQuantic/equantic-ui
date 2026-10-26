- **2026-10-25 · The board's Roadmap shows real dates and a forecast measured on the board**: the
  view was empty, and nothing on the board carried a size, so a count of issues or of pull requests
  dated 1.0 for the next day. Issues carry points now, Fibonacci from 1 to 13 in the board's
  Estimate field, and a delivered issue nobody pointed gets the points of the lines a person wrote in
  the pull requests that closed it, the files a tool writes left out: the board's 361 delivered
  issues were pointed that way, and its 273 open ones by hand against them. `scripts/roadmap.py`
  measures the pace in points over 28 calendar days (1,141 points, 40.8 a day, on the day it
  landed), plans the open work with a lane per epic at the share each had, writes the real dates of
  what happened and the forecast of the rest on epics and features, and gives each milestone a plan
  date, a range from a thousand runs and the date it would land with all the pace. It never closes an
  issue: a feature here also collects the defects found around it, so one whose sub-issues are all
  closed is listed for a person, which kept the 1.0 security review (#306) open. The slices E3 to E5
  of the Element tree went on the board for 1.0 (#1093, #1094, #1095), and fourteen issues no epic
  held were typed and placed under one
  ([#1090](https://github.com/eQuantic/equantic-ui/issues/1090)). Proposed and archived through
  OpenSpec (`openspec/specs/working-agreement`).
