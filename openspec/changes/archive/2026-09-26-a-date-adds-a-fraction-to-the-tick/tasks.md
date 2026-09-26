# Tasks

## 1. A fraction lands on the tick

- [x] 1.1 Port `DateTime.AddUnits` and the saturating conversion of a double into a long in the runtime's date twins, for days through microseconds
- [x] 1.2 Add `DateTime.addMicroseconds`, `DateTimeOffset.addMilliseconds` and `DateTimeOffset.addMicroseconds`
- [x] 1.3 Take `TimeOnly.addHours` and `addMinutes` as one saturated product, wrapped by the day
- [x] 1.4 Remove `ticksFromUnit`, which nothing reads any more
- [x] 1.5 Check: conformance cases run on both sides, with sub-millisecond, negative and NaN counts, failing before the port

## 2. A range refused in .NET's words

- [x] 2.1 Refuse a count past what a date can move by, and a result outside the calendar in `addTicks`
- [x] 2.2 Refuse a `DateTimeOffset` whose UTC time leaves the calendar, after its clock time
- [x] 2.3 Check: conformance cases compare each message on both sides, and the edge where a fraction truncates to nothing

## 3. Documentation

- [x] 3.1 The served runtime's budget records the new size
- [x] 3.2 The wiki's SupportedFeatures date rows in English and Portuguese, published at the merge
- [x] 3.3 One `docs/LEDGER.md` line citing #422
- [x] 3.4 Archive this change in the same pull request
