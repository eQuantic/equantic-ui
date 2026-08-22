import { Box, BoxStyle, BuildContext, EdgeInsets, SizeValue, StatelessComponent } from "../runtime-exports";

export class Divider extends StatelessComponent {
    declare inset: string;
    declare axis: string;
    declare leadingInset: any;

    constructor(inset: any = 'none', axis: any = 'horizontal', props?: any) {
        super();
        if (inset !== undefined) this.inset = inset;
        if (axis !== undefined) this.axis = axis;
        if (this.inset === undefined) this.inset = 'none';
        if (this.axis === undefined) this.axis = 'horizontal';
        this.inset = inset;
        this.axis = axis;
        if (props && typeof props === 'object') Object.assign(this, props);
    }

    build(context: BuildContext) {
        let line = new Box(new BoxStyle({ width: this.axis === 'vertical' ? SizeValue.fixed(1) : SizeValue.fill, height: this.axis === 'vertical' ? SizeValue.fill : SizeValue.fixed(1), background: context.theme.border }));
        if (this.inset === 'none') return line;
        let padding = this.inset === 'leading' ? new EdgeInsets(this.leadingInset ?? 16, 0, 0, 0) : EdgeInsets.symmetric(16, 0);
        return new Box(new BoxStyle({ width: SizeValue.fill, padding: padding }), line);
    }
}

