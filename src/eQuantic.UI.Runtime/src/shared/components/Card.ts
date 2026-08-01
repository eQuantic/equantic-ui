import { Box, BoxStyle, BuildContext, Color, ColorToken, CornerRadii, EdgeInsets, SizeValue, StatelessComponent, VisualNode } from "../runtime-exports";

export class Card extends StatelessComponent {
    declare child: VisualNode;
    declare kind: string;
    declare padding: EdgeInsets;
    declare width: SizeValue;
    constructor(child?: any, kind: any = 'elevated', props?: any) {
        super(props);
        if (child !== undefined) this.child = child;
        if (kind !== undefined) this.kind = kind;
        if (this.kind === undefined) this.kind = 'elevated';
        if (this.padding === undefined) this.padding = EdgeInsets.all(16);
        this.child = child;this.kind = kind;
    }

    build(context: BuildContext) {
        let theme = context.theme;let background = this.kind === 'filled' ? theme.surfaceSubtle : theme.surface;let borderWidth = this.kind === 'outlined' ? 1 : this.kind === 'elevated' ? 1 : 0;let borderColor = this.kind === 'elevated' ? new ColorToken(Color.transparent, theme.border.dark) : theme.border;return new Box(new BoxStyle({ width: this.width, padding: this.padding, background: background, cornerRadius: new CornerRadii(theme.shape('large')), borderWidth: borderWidth, borderColor: borderColor, elevation: this.kind === 'elevated' ? 1 : 0 }), this.child);
    }

}

