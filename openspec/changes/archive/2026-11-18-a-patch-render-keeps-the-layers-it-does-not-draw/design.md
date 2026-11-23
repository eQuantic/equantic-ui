# Design

## How Flutter solves it

Flutter has no layer that leaves because a frame did not paint it: an overlay is an entry in the list
Flutter's `Overlay` holds, and it leaves when the list stops holding it. Our layers are built lazily, when
the realizer shows them, so they leave when a frame does not show them. That is right only for a frame
that could have shown them, and a patch render draws only parts of the page.

## The choice

The issue named two cures. Showing again, in a patch, every layer it did not lower would build their
marked positions with the configuration of the last whole frame, and would count as shown a layer inside
a patched component that the patch then found closed, so it would never leave. Telling the owner which
parts the frame drew keeps the owner's rule as it is and makes it exact: a frame judges the layers it
drew. Photon, the server and a whole web render draw the whole tree and say nothing.

A position marked inside a layer the patch does not draw waits for that layer. The patch could show it
and lower what it built, but that is a second patch path for a layer's components; a whole render
already shows every open layer, so the patch hands that render over. It costs a whole render for a
press inside an open layer, which is what every render cost before #1455.
