(() => {
    const groups = {
        running: ['Created', 'Running', 'Waiting', 'Compensating'], idle: ['Idle'],
        completed: ['Completed', 'Compensated'], deadLettered: ['DeadLettered'],
        failed: ['Stopped', 'CompletedWithErrors', 'Aborted', 'Failed']
    };
    const utc = value => new Date(typeof value === 'string' && !/(?:Z|[+-]\d\d:\d\d)$/i.test(value) ? `${value}Z` : value);
    function clear(root, message = 'Loading changes…') {
        root.querySelectorAll('[data-metric-spark]').forEach(target => { target.textContent = message; target.classList.add('spark-empty'); });
        root.querySelectorAll('[data-metric-trend]').forEach(target => { target.hidden = true; });
    }
    function render(root, buckets, counts) {
        if (!buckets.length) return;
        const series = {};
        Object.entries(groups).forEach(([key, statuses]) => {
            series[key] = buckets.map(date => statuses.reduce((sum, status) => sum + (counts.get(`${date.getTime()}|${status}`) || 0), 0));
        });
        series.total = buckets.map((_, index) => Object.values(series).reduce((sum, values) => sum + values[index], 0));
        Object.entries(series).forEach(([key, values]) => {
            const target = root.querySelector(`[data-metric-spark="${key}"]`);
            if (!target) return;
            const total = values.reduce((sum, value) => sum + value, 0);
            const recorded = values.map((value, index) => value ? `${buckets[index].toISOString().slice(0, 16).replace('T', ' ')} UTC: ${value.toLocaleString()}` : '').filter(Boolean);
            target.title = 'Projected status changes for instances started in this period; this is not a history of the current instance count. Older transitions may be unavailable.'
                + (recorded.length ? `\n${recorded.join('\n')}` : '');
            if (!total) { target.textContent = 'No recorded transitions'; target.classList.add('spark-empty'); return; }
            target.classList.remove('spark-empty');
            const fleet = root.hasAttribute('data-runtime-catalog');
            const width = fleet ? 76 : 96, height = fleet ? 42 : 38, pad = 3;
            const min = Math.min(...values), max = Math.max(...values), range = Math.max(1, max - min);
            const points = values.map((value, index) => [pad + index * (width - 2 * pad) / Math.max(1, values.length - 1), height - pad - (value - min) / range * (height - 2 * pad)]);
            if (points.length === 1) points.push([width - pad, points[0][1]]);
            const line = points.map((p, i) => `${i ? 'L' : 'M'}${p[0].toFixed(1)} ${p[1].toFixed(1)}`).join(' ');
            const last = points[points.length - 1], css = fleet ? 'fleet' : 'metric';
            const gradient = `runtime-spark-${key}`;
            target.innerHTML = `<svg class="${css}-spark${fleet ? '' : 'line'}" viewBox="0 0 ${width} ${height}" preserveAspectRatio="none" role="img" aria-label="${total.toLocaleString()} projected state changes for instances started in the selected period"><defs><linearGradient id="${gradient}" x1="0" y1="0" x2="0" y2="1"><stop offset="0" stop-color="currentColor" stop-opacity=".28"></stop><stop offset="1" stop-color="currentColor" stop-opacity="0"></stop></linearGradient></defs><path class="${css}-spark-area" style="fill:url(#${gradient});opacity:1" d="${line} L${last[0]} ${height} L${points[0][0]} ${height} Z"></path><path class="${css}-spark-line" d="${line}"></path><circle cx="${last[0]}" cy="${last[1]}" r="2.3"></circle></svg><small class="spark-caption">${total.toLocaleString()} transitions</small>`;
            const trend = root.querySelector(`[data-metric-trend="${key}"]`);
            if (trend) trend.hidden = true;
        });
    }
    function renderResult(root, result) {
        const start = utc(result.fromUtc), before = utc(result.beforeUtc), bucket = String(result.bucket || '').toLowerCase();
        if (!Number.isFinite(start.getTime()) || !Number.isFinite(before.getTime())) return;
        const current = new Date(start); current.setUTCSeconds(0, 0);
        if (bucket !== 'minute') current.setUTCMinutes(0);
        if (bucket === 'day' || bucket === 'week') current.setUTCHours(0);
        if (bucket === 'week') current.setUTCDate(current.getUTCDate() - (current.getUTCDay() + 6) % 7);
        const buckets = [];
        while (current < before && buckets.length < 1000) {
            buckets.push(new Date(current));
            if (bucket === 'minute') current.setUTCMinutes(current.getUTCMinutes() + 1);
            else if (bucket === 'hour') current.setUTCHours(current.getUTCHours() + 1);
            else current.setUTCDate(current.getUTCDate() + (bucket === 'week' ? 7 : 1));
        }
        const counts = new Map();
        (result.activity || []).forEach(item => { const date = utc(item.bucketUtc); if (Number.isFinite(date.getTime())) counts.set(`${date.getTime()}|${item.status}`, Number(item.count || 0)); });
        render(root, buckets, counts);
    }
    window.KrackendRuntimeSparklines = { render, renderResult, clear };
})();
