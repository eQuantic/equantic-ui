# Design

## How Flutter solves it

Flutter never finds an element by the configuration it was built from. Build, layout and paint all walk
the element tree, so one configuration placed at two positions is two elements, each reached from its
parent. A configuration whose `build` returns itself recurses in Flutter until the stack overflows:
Flutter has no bound, and ours is the boundary's (`ComponentBoundary`, #1452).

## The choice

Photon walks positions too: its layout carries the element it measures down to the child it measures.
The web lowering walks nodes, and asks the frame for a component's position. The frame now answers from
the component position whose build is being lowered, so the walk descends positions as Photon's does,
without threading an element through every node kind's lowering.

The alternative the issue first suggested, counting the lowering's walk with the boundary's counter, was
rejected: the walk would still read the outermost position at every step, the bound would contain the
chain a second time, and the failure would be reported twice for one frame.
