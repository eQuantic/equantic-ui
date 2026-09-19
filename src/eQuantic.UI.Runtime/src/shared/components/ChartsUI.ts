import { BarChart, CategoryAxis, ChartSeries, ValueAxis } from "../runtime-exports";

export class ChartsUI {
    static barChart(series: ChartSeries[], categories: CategoryAxis, values: ValueAxis | null = null, layout: string = 'grouped', orientation: string = 'vertical', title: string | null = null, subtitle: string | null = null, plotHeight: number = 240) {
        return new BarChart(series, categories, values, layout, orientation, title, subtitle, plotHeight);
    }

    static chartSeries(Name: string, Values: number[], Slot: number = -1) {
        return new ChartSeries(Name, Values, Slot);
    }

    static categoryAxis(Categories: string[], Title: string | null = null) {
        return new CategoryAxis(Categories, Title);
    }

    static valueAxis(Title: string | null = null, Min: number | null = null, Max: number | null = null, Format: string = 'N0', Ticks: number = 5) {
        return new ValueAxis(Title, Min, Max, Format, Ticks);
    }
}

