function createChart(key, data) {
    const ctx = document.getElementById(key);

    var ticksCallbacks = data?.options?.scales
    console.log("callbacks gotten: ", ticksCallbacks);

    if (ticksCallbacks) {
        for (const [key, value] of Object.entries(ticksCallbacks)) {
            var callback = ticksCallbacks[key]?.ticks?.callback;
            if (callback) {
                console.log("Setting callback of string: ", callback);
                data.options.scales[key].ticks.callback = new Function("label", "index", "labels", callback);
            }
        }
    }

    console.log("doglas", key, data)

    new Chart(ctx, data);
}

