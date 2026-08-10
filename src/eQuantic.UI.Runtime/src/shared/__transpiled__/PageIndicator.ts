import { Box, BoxStyle, BuildContext, CornerRadii, EdgeInsets, Motion, Pressable, Row, SizeValue, StatelessComponent, Text, TransitionSpec, VisualNode } from "@equantic/runtime";

export class PageIndicator extends StatelessComponent {
    static maxDots: number = 8;
    declare count: number;
    declare currentIndex: number;
    declare onSelected: any;
    declare variant: string;
    constructor(count?: any, currentIndex?: any, onSelected: any = null, props?: any) {
        super();
        if (count !== undefined) this.count = count;
        if (currentIndex !== undefined) this.currentIndex = currentIndex;
        if (onSelected !== undefined) this.onSelected = onSelected;
        if (this.variant === undefined) this.variant = 'primary';
        this.count = count;this.currentIndex = currentIndex;this.onSelected = onSelected;
        if (props && typeof props === 'object') Object.assign(this, props);
    }

    build(context: BuildContext) {
        let theme = context.theme;let active = theme.colors(this.variant).base;let label = `Page ${this.currentIndex + 1} of ${this.count}`;if (this.count > PageIndicator.maxDots) {return new Text(label, 'caption', theme.textSecondary, 1, { tabular: true });}let row = new Row(4, 'start', 'center', false, null, null, { cross: 'center' });for (let i = 0; i < this.count; i++) {let index = i;let current = index === this.currentIndex;let dot = new Box(new BoxStyle({ width: current ? 18 : 6, height: 6, background: current ? active : theme.borderStrong, cornerRadius: new CornerRadii(theme.shape('full')), transition: TransitionSpec.of(1 | 32, Motion.state) }));row.add(this.onSelected == null ? dot : new Pressable(PageIndicator.hitPadded(dot), () => this.onSelected(index), { label: `Page ${index + 1}`, selected: current }));}return row;
    }

    static hitPadded(dot: VisualNode) {
        let centered = new Row(0, 'start', 'center', false, null, null, { width: SizeValue.fill, height: SizeValue.fill, main: 'center', cross: 'center' });centered.add(dot);return new Box(new BoxStyle({ height: 48, padding: EdgeInsets.symmetric(4, 0) }), centered);
    }

}

