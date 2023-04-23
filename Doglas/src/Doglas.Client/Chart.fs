module Doglas.Client.Chart

type Chart =
    {
        ``type``: string
        data: Data
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

