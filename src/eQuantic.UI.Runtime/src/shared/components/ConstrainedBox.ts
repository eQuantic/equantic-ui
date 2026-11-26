import { $eq, Box, BoxConstraints, BoxStyle, BuildContext, SizeValue, StatelessComponent, VisualNode } from "../runtime-exports";

export class ConstrainedBox extends StatelessComponent {
    static $typeId = 'eQuantic.UI.Components.ConstrainedBox';
    declare constraints: BoxConstraints;
    declare child: VisualNode;

    constructor(constraints?: any, child?: any, props?: any) {
        super();
        if (constraints !== undefined) this.constraints = constraints;
        if (child !== undefined) this.child = child;
        $eq.guard.throwIfNull(constraints, 'constraints');
        this.constraints = constraints;
        this.child = child;
        if (props && typeof props === 'object') Object.assign(this, props);
    }

    build(_context: BuildContext) {
        return new Box(new BoxStyle({ minWidth: this.constraints.minWidth, minHeight: this.constraints.minHeight, maxWidth: this.constraints.hasBoundedWidth ? SizeValue.fixed(this.constraints.maxWidth) : undefined, maxHeight: this.constraints.hasBoundedHeight ? SizeValue.fixed(this.constraints.maxHeight) : undefined }), this.child);
    }
}

