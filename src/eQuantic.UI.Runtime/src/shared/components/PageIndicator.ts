import { $eq, Box, BoxStyle, BuildContext, CornerRadii, EdgeInsets, Motion, Pressable, Row, SizeValue, StatelessComponent, Text, TransitionSpec, VariantValue, VisualNode } from "../runtime-exports";

export class PageIndicator extends StatelessComponent {
    static $typeId = 'eQuantic.UI.Components.PageIndicator';
    static maxDots: number = 8;
    declare count: number;
    declare currentIndex: number;
    declare onSelected: ((int: number) => void) | null;
    declare variant: VariantValue;

    constructor(count?: any, currentIndex?: any, onSelected: any = null, props?: any) {
        super();
        if (count !== undefined) this.count = count;
        if (currentIndex !== undefined) this.currentIndex = currentIndex;
        if (onSelected !== undefined) this.onSelected = onSelected;
        if (this.count === undefined) this.count = 0;
        if (this.currentIndex === undefined) this.currentIndex = 0;
        if (this.variant === undefined) this.variant = 'primary';
        this.count = count;
        this.currentIndex = currentIndex;
        this.onSelected = onSelected;
        if (props && typeof props === 'object') Object.assign(this, props);
    }

    build(context: BuildContext) {
        let theme = context.theme;
        let active = theme.colors(this.variant).base;
        let label = `Page ${$eq.text.format(this.currentIndex + 1, null, undefined, undefined, 'int32')} of ${$eq.text.format(this.count, null, undefined, undefined, 'int32')}`;
        let slot = theme.navigation;
        if (this.count > PageIndicator.maxDots) {
            return new Text(label, 'caption', slot?.pageColor ?? theme.textSecondary, 1, 'start', false, false, null, 0, { tabular: true });
        }
        let dotSize = slot?.dotSize ?? 6;
        let row = new Row(slot?.dotGap ?? 4, 'start', 'center', false, null, null, { cross: 'center' });
        for (let i = 0; i < this.count; i++) {
            let index = i;
            let current = index === this.currentIndex;
            let dot = new Box(new BoxStyle({ width: current ? slot?.currentDotWidth ?? 18 : dotSize, height: dotSize, background: current ? active : slot?.dotColor ?? theme.borderStrong, cornerRadius: new CornerRadii(slot?.dotRadius ?? theme.shape('full')), transition: TransitionSpec.of(1 | 32, Motion.state) }));
            row.add(this.onSelected == null ? dot : new Pressable(PageIndicator.hitPadded(dot), () => this.onSelected!(index), { label: `Page ${$eq.text.format(index + 1, null, undefined, undefined, 'int32')}`, selected: current }));
        }
        return row;
    }

    static hitPadded(dot: VisualNode) {
        let centered = new Row(0, 'start', 'center', false, null, null, { width: SizeValue.fill, height: SizeValue.fill, main: 'center', cross: 'center' });
        centered.add(dot);
        return new Box(new BoxStyle({ height: 48, padding: EdgeInsets.symmetric(4, 0) }), centered);
    }
}

