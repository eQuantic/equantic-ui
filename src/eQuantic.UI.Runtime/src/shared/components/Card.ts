import { Box, BoxStyle, BuildContext, Color, ColorToken, CornerRadii, EdgeInsets, SizeValue, StatelessComponent, VisualNode } from "../runtime-exports";

export class Card extends StatelessComponent {
    static $typeId = 'eQuantic.UI.Components.Card';
    declare child: VisualNode;
    declare kind: string;
    declare padding: EdgeInsets;
    declare width: SizeValue;

    constructor(child?: any, kind: any = 'elevated', props?: any) {
        super();
        if (child !== undefined) this.child = child;
        if (kind !== undefined) this.kind = kind;
        if (this.kind === undefined) this.kind = 'elevated';
        if (this.padding === undefined) this.padding = EdgeInsets.all(16);
        this.child = child;
        this.kind = kind;
        if (props && typeof props === 'object') Object.assign(this, props);
    }

    build(context: BuildContext) {
        let theme = context.theme;
        let slot = theme.card;
        let background = this.kind === 'filled' ? slot?.filledColor ?? theme.surfaceSubtle : theme.surface;
        let borderWidth = (($0) => ($0 === 'outlined' || $0 === 'elevated'))(this.kind) ? slot?.borderWidth ?? 1 : 0;
        let borderColor = this.kind === 'elevated' ? slot?.elevatedBorderColor ?? new ColorToken(Color.transparent, theme.border.dark) : theme.border;
        return new Box(new BoxStyle({ width: this.width, padding: this.padding, background: background, cornerRadius: new CornerRadii(theme.shape(slot?.shape ?? 'large')), borderWidth: borderWidth, borderColor: borderColor, elevation: this.kind === 'elevated' ? slot?.elevation ?? 1 : 0 }), this.child);
    }
}

