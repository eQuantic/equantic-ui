# Tasks

## 1. Measure before changing

- [x] 1.1 Drive the real `draggable.ts` and `drag-dismiss.ts` in Chromium 145 with the pointer: a quarter-turned ancestor heard nothing in 40px down the screen; a 200px track turned by its own transform jumped to 200px and reported 1 after 40px sideways; under a parent at `scale(2)` a 40px move wrote 40px, drew 80 and reported 40; a sheet with a threshold of 96 under `scale(2)`, dragged 120px, was drawn 240 away and dismissed
- [x] 1.2 Read Photon's answers in `TransformedSurfaceTests`: 40 along a track turned a quarter, 20 and 0.1 at a scale of 2, a slop of 12 on the screen, a dismiss counted in its own space

## 2. A gesture on the web reads its own space

- [x] 2.1 `gestureSpace`: the inverse of the composed linear part of the ancestors' computed transforms, read at the press, and a slop measured on the screen
- [x] 2.2 The drag reads its travel through it and divides a normalized one by its untransformed box
- [x] 2.3 The dismiss reads its travel and its threshold through it
- [x] 2.4 Pin on Photon the case of a surface turned by its own transform: 40 and 0.2
- [x] 2.5 Prove it both ways: 8 new cases of `draggable.spec.ts` and 2 of `drag-dismiss.spec.ts` fail against the old controllers and pass; the most plausible wrong fixes fail too (the surface's own transform in the space, the ancestors composed the other way round, the slop read in the surface's space); and in Chromium 145 the same pages now draw the surface under the finger: 40 down, 0.2, 20 drawn 40, 8 past a slop of 16 on the screen, and the sheet drawn 120 away and kept

## 3. Documentation

- [x] 3.1 One ledger entry under `docs/ledger/` citing #1195, #1204 and #1492; the wiki states none of the gaps, so no page changes
