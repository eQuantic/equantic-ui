import { Box, BoxStyle, BuildContext, Column, Draggable, Icon, Positioned, Pressable, SizeValue, Stack, StatelessComponent, Text, VisualNode } from "../runtime-exports";

export class SwipeableRow extends StatelessComponent {
    static actionWidth: number = 96;
    declare child: VisualNode;
    declare actionLabel: string;
    declare actionIcon: string;
    declare onAction: (() => void) | null;
    declare actionVariant: string;
    declare open: boolean;
    declare onOpenChanged: any;
    constructor(child?: any, actionLabel?: any, actionIcon?: any, onAction: any = null, props?: any) {
        super();
        if (child !== undefined) this.child = child;
        if (actionLabel !== undefined) this.actionLabel = actionLabel;
        if (actionIcon !== undefined) this.actionIcon = actionIcon;
        if (onAction !== undefined) this.onAction = onAction;
        if (this.actionIcon === undefined) this.actionIcon = 'search';
        if (this.actionVariant === undefined) this.actionVariant = 'destructive';
        this.child = child;this.actionLabel = actionLabel;this.actionIcon = actionIcon;this.onAction = onAction;
        if (props && typeof props === 'object') Object.assign(this, props);
    }

    build(context: BuildContext) {
        let theme = context.theme;let colors = theme.colors(this.actionVariant);let actionContent = new Column(3, 'start', 'stretch', false, null, null, { width: SizeValue.fill, height: SizeValue.fill, main: 'center', cross: 'center' });actionContent.add(new Icon(this.actionIcon, 20, colors.onBase));actionContent.add(new Text(this.actionLabel, 'caption', colors.onBase, 1));let action = new Pressable(new Box(new BoxStyle({ width: SwipeableRow.actionWidth, height: SizeValue.fill, background: colors.base }), actionContent), this.onAction, { label: this.actionLabel });let surface = new Box(new BoxStyle({ width: SizeValue.fill, background: theme.surface }), this.child);let revealed = new Stack();revealed.add(new Positioned(action, 0, 0, 0));revealed.add(new Draggable(surface, this.onReleased.bind(this), { axis: 'horizontal', min: -SwipeableRow.actionWidth, max: 0, restOffset: this.open ? -SwipeableRow.actionWidth : 0 }));return new Box(new BoxStyle({ width: SizeValue.fill, clip: true }), revealed);
    }

    onReleased(offset: number) {
        return this.onOpenChanged?.(offset <= -SwipeableRow.actionWidth / 2);
    }

}

