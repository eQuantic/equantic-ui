import { Box, BoxStyle, BuildContext, ControlMetrics, CornerRadii, EdgeInsets, Flexible, Icon, IconGlyph, Pressable, Row, SdkStrings, SizeValue, StatelessComponent, StyleDiff, TextEntry, VisualNodeExtensions } from "../runtime-exports";

export class SearchField extends StatelessComponent {
    static $typeId = 'eQuantic.UI.Components.SearchField';
    declare query: string;
    declare onChanged: ((string: string) => void) | null;
    declare placeholder: string;
    declare onSubmit: (() => void) | null;

    constructor(query?: any, onChanged: any = null, placeholder: any = null, onSubmit: any = null, props?: any) {
        super();
        if (query !== undefined) this.query = query;
        if (onChanged !== undefined) this.onChanged = onChanged;
        if (placeholder !== undefined) this.placeholder = placeholder;
        if (onSubmit !== undefined) this.onSubmit = onSubmit;
        this.query = query;
        this.onChanged = onChanged;
        this.placeholder = placeholder ?? SdkStrings.searchPlaceholder;
        this.onSubmit = onSubmit;
        if (props && typeof props === 'object') Object.assign(this, props);
    }

    build(context: BuildContext) {
        let theme = context.theme;
        let slot = theme.input;
        let height = ControlMetrics.height(context, 'medium', (slot?.controls ?? null));
        let fill = slot?.searchFill ?? theme.surfaceSubtle;
        let iconSize = slot?.iconSize ?? 20;
        let row = new Row(slot?.gap ?? 10, 'start', 'center', false, null, null, { height: SizeValue.fill, cross: 'center' });
        row.add(new Icon(IconGlyph.fromIcons('search'), iconSize, theme.textMuted));
        row.add(new Flexible(new TextEntry(this.query, this.onChanged, { placeholder: this.placeholder, label: this.placeholder.length > 0 ? this.placeholder : null, onSubmit: this.onSubmit, role: 'bodyM' }), 1));
        if (this.query.length > 0) {
            row.add(new Pressable(new Box(new BoxStyle({ width: 48, height: height, cornerRadius: new CornerRadii(theme.shape('full')), hover: new StyleDiff({ background: fill.midpointWith(theme.border) }), pressed: new StyleDiff({ background: theme.border }) }), VisualNodeExtensions.centered(new Icon(IconGlyph.fromIcons('close'), iconSize, theme.textMuted))), () => this.onChanged?.(''), { label: SdkStrings.clearSearch }));
        }
        return new Box(new BoxStyle({ width: SizeValue.fill, height: height, background: fill, cornerRadius: new CornerRadii(slot?.searchRadius ?? theme.shape('full')), padding: EdgeInsets.symmetric(slot?.paddingX ?? 14, 0) }), row);
    }
}

