module Doglas.Client.Chart

type Chart =
    {
        ``type``: string
        data: Data
        options: ChartOption option
    }
and Data =
    {
        datasets: DataSet list
    }
and DataSet =
    {
        label: string
        data: DataPoint list
    }
and DataPoint =
    {
        x: float
        y: float
    }
and ChartOption =
    {
        scales: ChartOptionScales
    }
and ChartOptionScales =
    {
        x: ChartOptionXAxis
    }
and ChartOptionXAxis =
    {
        ticks: ChartOptionTicks
    }
and ChartOptionTicks =
    {
        callback: string // function(label, index, labels) can be assumed. Just need to write in format of what returns. Ex: "return label;"
    }

