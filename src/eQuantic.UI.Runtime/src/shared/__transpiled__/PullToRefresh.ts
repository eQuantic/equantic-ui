import { Box, BoxStyle, BuildContext, Draggable, Positioned, Row, SizeValue, Spinner, Stack, StatelessComponent, VisualNode } from "@equantic/runtime";

export class PullToRefresh extends StatelessComponent {
    static threshold: number = 64;
    declare child: VisualNode;
    declare onRefresh: (() => void) | null;
    declare refreshing: boolean;
    constructor(child?: any, onRefresh: any = null, props?: any) {
        super(props);
        if (child !== undefined) this.child = child;
        if (onRefresh !== undefined) this.onRefresh = onRefresh;
        this.child = child;this.onRefresh = onRefresh;
    }

    build(context: BuildContext) {
        let theme = context.theme;let indicator = new Row(0, { width: SizeValue.fill, height: PullToRefresh.threshold, main: 'center', cross: 'center' });indicator.add(new Spinner(20, theme.colors('primary').base));let content = new Box(new BoxStyle({ width: SizeValue.fill, background: theme.background }), this.child);let stack = new Stack();stack.add(new Positioned(indicator, 0, 0, null, 0));stack.add(new Draggable(content, this.onReleased.bind(this), { axis: 'vertical', min: 0, max: PullToRefresh.threshold, restOffset: this.refreshing ? PullToRefresh.threshold : 0 }));return new Box(new BoxStyle({ width: SizeValue.fill, clip: true }), stack);
    }

    onReleased(offset: number) {
        if (offset >= PullToRefresh.threshold) this.onRefresh?.();
    }

}

