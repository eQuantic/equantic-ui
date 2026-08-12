import { Box, BoxStyle, BuildContext, CodeBlock, ColorToken, CornerRadii, EdgeInsets, IconGlyph, MermaidArrowhead, MermaidLayout, MermaidParser, MermaidPlacedNode, Positioned, ScrollView, SizeValue, Stack, StatelessComponent, Text, Vector } from "@equantic/runtime";

export class Mermaid extends StatelessComponent {
    static headSize: number = 9;
    declare source: string;
    constructor(source?: any, props?: any) {
        super();
        if (source !== undefined) this.source = source;
        this.source = source;
        if (props && typeof props === 'object') Object.assign(this, props);
    }

    build(context: BuildContext) {
        let theme = context.theme;let graph = MermaidParser.parse(this.source);if (graph == null) return new CodeBlock(this.source, 'mermaid', { showLineNumbers: false });let scene = MermaidLayout.solve(graph);let canvas = new Stack('topStart', { width: scene.width, height: scene.height });let ink = theme.borderStrong;for (const segment of scene.segments) canvas.add(new Positioned(new Box(new BoxStyle({ width: segment.w, height: segment.h, background: ink })), segment.y, null, null, segment.x));for (const arrow of scene.arrows) canvas.add(Mermaid.placeArrowhead(arrow, ink));for (const label of scene.labels) {let width = label.text.length * 6 + 16;canvas.add(new Positioned(new Box(new BoxStyle({ padding: new EdgeInsets(6, 1, 6, 1), background: theme.background, borderColor: theme.border, borderWidth: 1, cornerRadius: new CornerRadii(6) }), new Text(label.text, 'caption', theme.textMuted, 1)), label.y - 10, null, null, label.x - Math.trunc(width / 2)));}for (const placed of scene.nodes) canvas.add(new Positioned(Mermaid.nodeView(placed, theme), placed.y, null, null, placed.x));return new ScrollView(canvas, 'horizontal', { width: SizeValue.fill });
    }

    static nodeView(placed: MermaidPlacedNode, theme: any) {
        let node = placed.node;if (node.shape === 'diamond') return Mermaid.diamondView(placed, theme);let radius = (() => { const _s = node.shape; if (_s === 'circle') return placed.h / 2; if (_s === 'rounded') return placed.h / 2; return 6; })();return new Box(new BoxStyle({ width: placed.w, height: placed.h, background: theme.surface, borderColor: theme.borderStrong, borderWidth: 1, cornerRadius: new CornerRadii(radius), padding: EdgeInsets.symmetric(8, 0) }), new Text(node.label, 'label', theme.textPrimary, 2, 'center').centered());
    }

    static diamondView(placed: MermaidPlacedNode, theme: any) {
        let side = placed.w;let rhombus = new IconGlyph('mermaidDiamond', 'M50 0 L100 50 L50 100 L0 50 Z', 'fill', '0 0 100 100');let stack = new Stack('topStart', { width: side, height: side });stack.add(new Vector(rhombus, side, theme.borderStrong));stack.add(new Positioned(new Vector(rhombus, side - 6, theme.surface), 3, null, null, 3));stack.add(new Box(new BoxStyle({ width: side, height: side }), new Text(placed.node.label, 'labelSmall', theme.textPrimary, 2, 'center').centered()));return stack;
    }

    static headGlyph(direction: number) {
        if (direction === 1) return new IconGlyph('mermaidHeadRight', 'M0 0 V100 L100 50 Z', 'fill', '0 0 100 100');if (direction === 2) return new IconGlyph('mermaidHeadUp', 'M50 0 L100 100 H0 Z', 'fill', '0 0 100 100');if (direction === 3) return new IconGlyph('mermaidHeadLeft', 'M100 0 V100 L0 50 Z', 'fill', '0 0 100 100');return new IconGlyph('mermaidHeadDown', 'M0 0 H100 L50 100 Z', 'fill', '0 0 100 100');
    }

    static placeArrowhead(arrow: MermaidArrowhead, ink: ColorToken) {
        let half = (Mermaid.headSize - 1) / 2;let top = arrow.y - Mermaid.headSize;let start = arrow.x - half;if (arrow.direction === 1) {top = arrow.y - half;start = arrow.x - Mermaid.headSize;} else if (arrow.direction === 2) {top = arrow.y;start = arrow.x - half;} else if (arrow.direction === 3) {top = arrow.y - half;start = arrow.x;}return new Positioned(new Vector(Mermaid.headGlyph(arrow.direction), Mermaid.headSize, ink), top, null, null, start);
    }

}

