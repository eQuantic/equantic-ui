import { BarChart, CategoryAxis, ChartSeries, ValueAxis } from "@equantic/runtime";

export class ChartsUI {
    static barChart(series: ChartSeries[], categories: CategoryAxis, values: ValueAxis | null = null, layout: string = 'grouped', orientation: string = 'vertical', title: string | null = null, subtitle: string | null = null, plotHeight: number = 240) {
        return new BarChart(series, categories, values, layout, orientation, title, subtitle, plotHeight);
    }

    static chartSeries(name: string, values: number[], slot: number = -1) {
        return new ChartSeries(name, values, slot);
    }

    static categoryAxis(categories: string[], title: string | null = null) {
        return new CategoryAxis(categories, title);
    }

    static valueAxis(title: string | null = null, min: number | null = null, max: number | null = null, format: string = 'N0', ticks: number = 5) {
        return new ValueAxis(title, min, max, format, ticks);
    }
}

