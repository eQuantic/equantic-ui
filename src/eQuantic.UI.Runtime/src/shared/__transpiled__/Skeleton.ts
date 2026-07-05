import { $eq, Box, BoxStyle, BuildContext, Component, ComponentContext, CornerRadii, HtmlElement, Radius, StatelessComponent, VisualNode } from "@equantic/runtime";

export class Skeleton extends StatelessComponent {
    constructor(shape?: any, width?: any, height: any = 0, props?: any) {
        super(props);
        if (shape !== undefined) this.shape = shape;
        if (width !== undefined) this.width = width;
        if (height !== undefined) this.height = height;
        this.shape = shape;this.width = width;this.height = height;
    }

    build(context: BuildContext) {
        let height = (() => { const _s = this.shape; if (_s === 'line') return 12; if (_s === 'circle') return this.width; return this.height > 0 ? this.height : this.width; })();let radius = (() => { const _s = this.shape; if (_s === 'line') return Radius.full; if (_s === 'circle') return Radius.full; return Radius.md; })();return new Box(new BoxStyle({ width: this.width, height: height, background: context.theme.surfaceSubtle, cornerRadius: new CornerRadii(radius) }));
    }

}

