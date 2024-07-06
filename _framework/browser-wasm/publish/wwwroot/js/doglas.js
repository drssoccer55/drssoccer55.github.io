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

var memoize = function (passedFunc) {
    var cache = {};
    return function (x) {
        if (x in cache) return cache[x];
        return cache[x] = passedFunc(x);
    };
}

var canvasMem = memoize(function (key) {
    return document.getElementById(key);
});

var canvas2dContext = memoize(function (key) {
    let element = document.getElementById(key);
    return element.getContext("2d");
});

function draw(key, drawContext) {
    console.log("draw", key, drawContext)
    let canvas = canvasMem(key)
    let ctx = canvas2dContext(key);
    if (drawContext.mouseDown) {
        let ol = canvas.offsetLeft;
        let ot = canvas.offsetTop;

        ctx.beginPath();
        ctx.moveTo(drawContext.prevX - ol + window.pageXOffset, drawContext.prevY - ot + window.pageYOffset);
        ctx.lineTo(drawContext.curX - ol + window.pageXOffset, drawContext.curY - ot + window.pageYOffset);
        ctx.strokeStyle = drawContext.style;
        ctx.lineWidth = drawContext.width;
        ctx.stroke();
        ctx.closePath();
    }
}

function clear(key) {
    let canvas = canvasMem(key)
    let ctx = canvas2dContext(key);
    ctx.clearRect(0, 0, canvas.width, canvas.height);
}

function getCanvasData(key) {
    let canvas = canvasMem(key)
    return canvas.toDataURL();
}

