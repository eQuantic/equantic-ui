import { Box, BuildContext, Button, CultureOption, CuratedIcons, Menu, MenuItem, SegmentedControl, SizeVariantValue, StatelessComponent } from "../runtime-exports";

export class CultureSwitcher extends StatelessComponent {
    declare options: CultureOption[];
    declare size: SizeVariantValue;
    declare onChanged: ((string: string) => void) | null;
    constructor(options?: any, props?: any) {
        super();
        if (options !== undefined) this.options = options;
        if (this.size === undefined) this.size = 'small';
        this.options = options;
        if (props && typeof props === 'object') Object.assign(this, props);
    }

    build(context: BuildContext) {
        if (this.options.length === 0) return new Box();
        let controller = context.getService('ICultureController');
        let current = controller?.uICulture ?? '';
        let selected = 0;
        for (let i = 0; i < this.options.length; i++) {
            if (this.options[i].name === current) {
                selected = i;
                break;
            }
            if (CultureSwitcher.languageOf(this.options[i].name) === CultureSwitcher.languageOf(current)) selected = i;
        }
        if (this.options.length <= 3) {
            let labels: string[] = [];
            for (const option of this.options) labels.push(option.label);
            return new SegmentedControl(labels, selected, (index: number) => this.switch(controller, index), { size: this.size, stretch: false });
        }
        let items: MenuItem[] = [];
        for (const option of this.options) items.push(new MenuItem(option.label));
        return new Menu(new Button(this.options[selected].label, 'outline', this.size, null, { leading: CuratedIcons.resolve('chevronDown') }), items, (index: number) => this.switch(controller, index));
    }

    switch(controller: any, index: number) {
        if (index < 0 || index >= this.options.length) return;
        let option = this.options[index];
        controller?.apply(option.name, option.name);
        this.onChanged?.(option.name);
    }

    static languageOf(name: string) {
        let cut = name.indexOf('-');
        return cut < 0 ? name : name.slice(0, cut);
    }

}

