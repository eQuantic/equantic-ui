import { Box, BoxStyle, BuildContext, EdgeInsets, SizeValue, StatelessComponent } from "../runtime-exports";

export class Divider extends StatelessComponent {
    static $typeId = 'eQuantic.UI.Components.Divider';
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
        let slot = context.theme.list;
        let thickness = slot?.dividerThickness ?? 1;
        let line = new Box(new BoxStyle({ width: this.axis === 'vertical' ? SizeValue.fixed(thickness) : SizeValue.fill, height: this.axis === 'vertical' ? SizeValue.fill : SizeValue.fixed(thickness), background: slot?.dividerColor ?? context.theme.border }));
        if (this.inset === 'none') return line;
        let inset = slot?.dividerInset ?? 16;
        let padding = this.inset === 'leading' ? new EdgeInsets(this.leadingInset ?? inset, 0, 0, 0) : EdgeInsets.symmetric(inset, 0);
        return new Box(new BoxStyle({ width: SizeValue.fill, padding: padding }), line);
    }
}

