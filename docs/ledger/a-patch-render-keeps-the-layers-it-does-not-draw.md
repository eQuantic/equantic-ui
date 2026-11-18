- **2026-11-16 · A patch render keeps the layers it does not draw**: a render the browser answered by lowering
  again only the components that built ended the owner's frame as if it had drawn the whole page, so an open
  overlay or anchored panel elsewhere had its components unmounted while its DOM stayed, dead, and a press inside
  an open layer killed it the same way. A realizer now says which parts of the tree a frame drew
  (`BuildOwner.DrewOnly`) and the frame judges only the layers among them; a press inside a layer lowers the page
  whole, which shows the layer ([#1575](https://github.com/eQuantic/equantic-ui/issues/1575)). Proposed and
  archived through OpenSpec (`openspec/specs/element-tree`).
