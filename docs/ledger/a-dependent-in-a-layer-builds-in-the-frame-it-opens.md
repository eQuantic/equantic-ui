- **2026-11-12 · A dependent inside a layer builds in the frame the layer opens**: a component that reads an
  inherited value through DependOn built one frame late when the value sat inside an overlay's layer and changed in
  the frame the layer was shown, because the realizer builds a layer after the frame's build phase and a dependent
  marked then waited for the next frame ([#1413](https://github.com/eQuantic/equantic-ui/issues/1413)). It now builds
  in the layer's BuildDeferred, by depth, before the layer is laid out, and no frame is asked for after it. Pinned in
  InheritedComponentTests.
