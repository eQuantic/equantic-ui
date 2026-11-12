# Proposal

Closes #1404, the web half of E4b, under #1094 (E4) and #937, the element tree. The error boundary's report and tally follow in E4c (#1452).

## Why

A render on the web had five channels it reached through the async flow: the atomic style sink, the gradient sink, the face sink, the expansion scope that names each component the render expands, and the link policy that prefixes an in-app destination with the request's culture. The density the browser reported was a sixth, read by the escape hatch's bridge. The server armed each of them around a render and cleared it after, so any code running in the request's flow could read or overwrite them, and a render that forgot one inherited whatever the flow carried. E4a (#1403) gave the request an owner, and a render's channels belong to that render, not to an async flow (docs/ELEMENT-TREE-PLAN.md, 3.5).

## What Changes

- **A render's channels are one object it is handed.** `RenderChannels` holds the three sinks, the expansion scope, the density and the link policy. The server makes one per prefetch round and one per discovery, and hands it to `MarkupHost.Lower`, which hands it to the lowering visitor.
- **The escape hatch's bridge is the one reader of an ambient.** An `HtmlElement`'s `Render()` takes no parameter, so a write-once bridge inside a page drawn by hand cannot be handed anything. The server renders such a page with its round's channels armed by `RenderChannels.BridgeWith`, and the bridge reads them there. Changing `Render()` would change the escape hatch's whole surface, which crosses to the browser too.
- **The six ambients go.** `StyleSink.Ambient`, `GradientSink.Ambient`, `FaceSink.Ambient`, `ComponentExpansionScope.Ambient`, `VisualNodeComponent.AmbientDensity`, and `RenderContext.SetLinkPolicy` with its `LinkPolicy` and `ResolveDestination`. What `ResolveDestination` answered, `RenderChannels.ResolveDestination` answers.
- **`MarkupHost.Lower` takes the render's channels** instead of a style sink.
- **Every `AsyncLocal` the source keeps is named, with its reason**, in a test that fails on one it does not name and on a name the source no longer declares. The list may only shrink.

The markup, the SSR fixtures and the hydration payload do not change. An app writes nothing new. The public surface moves in the web realizer only: the six ambients go, `RenderChannels` arrives, and `MarkupHost.Lower` takes it. No developer surface moves. The migration line, for code that armed a sink around a render: make a `RenderChannels`, hand it to `MarkupHost.Lower`, or arm it with `RenderChannels.BridgeWith` around an escape-hatch page, and read its sinks after.

## Impact

The web realizer, the server's rendering service and the component tester. The proof is the server's SSR fixtures and the hydration contract byte for byte, the component parity fixture with a page in a language lowered through its channels, two requests at different densities drawn at once each writing their own page, and the list of ambient channels.
