# Tasks

## 1. Measure before changing

- [x] 1.1 Drive the real `draggable.ts` in Chrome 152 over a 200px surface at `scale(.5)` with a rest of 0.25: at rest x 400, 389 after 14px, 415 after 40px, released at 0.65
- [x] 1.2 Read the issue's cases on Photon: `TransformedSurfaceTests` at 2× and a quarter turn, against the host as it stood

## 2. A gesture reads its own space

- [x] 2.1 Hold the inverse the press found, and read a drag's and a pan's every point through it
- [x] 2.2 Take the drag's extent from the region's own rect
- [x] 2.3 On the web, divide by the rect without the surface's own scale and write back in the box
- [x] 2.4 Prove it both ways: 8 cases of `TransformedSurfaceTests` fail against af3f714e0 and pass; `draggable.spec.ts`; and the same page in Chrome 152: x 400, 414 after 14px, 440 after 40px, released at 0.45

## 3. Documentation

- [x] 3.1 One ledger entry under `docs/ledger/` citing #702 and #734; the wiki states neither gap, so no page changes
