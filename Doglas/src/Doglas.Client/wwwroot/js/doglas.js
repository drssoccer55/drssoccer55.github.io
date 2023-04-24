function createChart(key, data) {
    const ctx = document.getElementById(key);

    console.log("doglas", key, data)

    new Chart(ctx, data);
}

