(() => {
    const charts = new Map();
    const lastSeries = new Map();
    const observers = new Map();
    const colors = () => {
        const style = getComputedStyle(document.querySelector('.runtime-reference') || document.documentElement);
        const read = (name, fallback) => style.getPropertyValue(name).trim() || fallback;
        return { text: read('--text', '#15251f'), muted: read('--muted', '#52675e'),
            border: read('--border', '#d6e2dc'), surface: read('--surface', '#fff'),
            primary: read('--primary', '#087a57'), success: read('--success', '#087a57'),
            warning: read('--warning', '#c98718'), danger: read('--danger', '#b4233f'),
            info: read('--info', '#285eb8') };
    };
    function replace(id, series) {
        const element = document.getElementById(id);
        if (!element?.hasAttribute('data-echart-kind')) return false;
        lastSeries.set(id, series);
        if (!window.echarts) { element.textContent = 'Chart library is unavailable.'; return true; }
        let chart = charts.get(id);
        if (!chart) {
            chart = echarts.init(element);
            charts.set(id, chart);
            let frame = 0, width = 0, height = 0;
            const observer = new ResizeObserver(entries => {
                const rect = entries[0]?.contentRect;
                if (!rect || (rect.width === width && rect.height === height)) return;
                width = rect.width; height = rect.height;
                cancelAnimationFrame(frame);
                frame = requestAnimationFrame(() => { if (!chart.isDisposed()) chart.resize(); });
            });
            observer.observe(element);
            observers.set(id, () => { observer.disconnect(); cancelAnimationFrame(frame); });
        }
        const c = colors(), kind = element.dataset.echartKind;
        const option = { animationDuration: 400, textStyle: { color: c.text },
            aria: { enabled: true }, tooltip: { trigger: 'item' } };
        if (kind === 'activity') {
            const points = series[0]?.points || [];
            option.color = [c.success, '#c98718', c.danger];
            option.legend = { right: 12, top: 2, itemWidth: 8, itemHeight: 8, textStyle: { color: c.muted, fontSize: 9 } };
            option.tooltip = { trigger: 'axis', backgroundColor: c.surface, borderColor: c.border,
                textStyle: { color: c.text, fontSize: 10 }, extraCssText: 'box-shadow:0 10px 28px rgba(17,24,39,.14);border-radius:8px', formatter: params => {
                const safe = text => String(text ?? '').replace(/[&<>"']/g, ch => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[ch]));
                const partial = params[0]?.data?.partial ? ' · partial' : '';
                return `<strong>${safe(params[0]?.axisValueLabel)} UTC${partial}</strong><br>${params.map(p => `${p.marker}${safe(p.seriesName)}<b style="float:right;margin-left:18px">${Number(p.data?.raw || 0).toLocaleString()}</b>`).join('<br>')}`;
            } };
            option.grid = { left: 14, right: 16, top: 38, bottom: 12, containLabel: true };
            option.xAxis = { type: 'category', data: points.map(p => p.label), axisLine: { lineStyle: { color: c.border } }, axisTick: { show: false }, axisLabel: { color: c.muted, fontSize: 8 }, boundaryGap: false };
            const maxValue = Math.max(0, ...series.flatMap(s => s.points.map(p => p.value)));
            option.yAxis = { type: 'value', min: 0, max: Math.max(1, Math.ceil(Math.log10(maxValue + 1))), interval: 1, name: 'events · logarithmic', nameTextStyle: { color: c.muted, fontSize: 7 },
                axisLine: { show: false }, axisTick: { show: false }, axisLabel: { color: c.muted, fontSize: 8, formatter: n => Math.round(10 ** n - 1).toLocaleString() }, splitLine: { lineStyle: { color: c.border, type: 'dashed' } } };
            option.series = series.map((s, i) => ({ name: s.name, type: 'line', smooth: i ? .3 : .35, symbol: 'none', showSymbol: false, lineStyle: { width: i ? 2 : 2.4 },
                data: s.points.map(p => ({ value: Math.log10(Math.max(0, p.value) + 1), raw: p.value, partial: p.partial })), areaStyle: i === 0 ? { opacity: .15 } : undefined }));
        } else if (kind === 'status') {
            const order = ['Completed', 'Running', 'Failed', 'Dead lettered', 'Idle'];
            const colorsByName = { 'Completed': c.success, 'Running': c.info, 'Failed': c.danger, 'Dead lettered': '#c98718', 'Idle': c.warning };
            const values = series.map(s => ({ name: s.name, value: s.points[0]?.value || 0,
                itemStyle: { color: colorsByName[s.name] || c.primary } })).sort((a, b) => order.indexOf(a.name) - order.indexOf(b.name));
            const total = values.reduce((sum, item) => sum + item.value, 0);
            option.title = { text: total.toLocaleString(), subtext: 'instances', left: 'center', top: '35%', textStyle: { color: c.text, fontSize: 22, fontWeight: 750 }, subtextStyle: { color: c.muted, fontSize: 8, lineHeight: 15 } };
            option.legend = { bottom: 5, left: 'center', textStyle: { color: c.muted, fontSize: 8 }, itemWidth: 8, itemHeight: 8 };
            option.series = total ? [{ type: 'pie', radius: ['51%', '72%'], center: ['50%', '43%'], label: { show: false },
                itemStyle: { borderColor: c.surface, borderWidth: 2, borderRadius: 4 }, data: values }] : [];
        } else if (kind === 'gauge') {
            const value = series[0]?.points[0]?.value;
            option.series = value == null ? [] : [{ type: 'gauge', center: ['50%', '64%'], radius: '90%', startAngle: 200, endAngle: -20,
                progress: { show: true, width: 12, roundCap: true, itemStyle: { color: value >= 90 ? c.success : value >= 75 ? c.warning : c.danger } },
                axisLine: { lineStyle: { width: 12, color: [[1, c.border]] } }, axisTick: { show: false }, splitLine: { show: false },
                axisLabel: { show: false }, pointer: { show: false }, title: { offsetCenter: [0, '35%'], color: c.muted, fontSize: 8 }, detail: { formatter: '{value}%', offsetCenter: [0, '3%'], color: c.text, fontSize: 22, fontWeight: 750 }, data: [{ value, name: 'Success rate' }] }];
        } else if (kind === 'funnel') {
            option.series = [{ type: 'funnel', left: '12%', width: '76%', top: 20, bottom: 20,
                label: { color: c.text }, data: (series[0]?.points || []).map((p, i) => ({ name: p.label, value: p.value,
                    itemStyle: { color: i ? c.success : c.primary } })) }];
        } else {
            const points = (series[0]?.points || []).slice();
            if (kind !== 'causes') points.reverse();
            option.grid = { left: 12, right: kind === 'causes' ? 58 : 24, top: 8, bottom: 10, containLabel: true };
            option.xAxis = { type: 'value', show: kind !== 'causes', minInterval: 1, splitLine: { lineStyle: { color: c.border, type: 'dashed' } }, axisLabel: { color: c.muted, fontSize: 7 }, axisLine: { show: false }, axisTick: { show: false } };
            option.yAxis = { type: 'category', inverse: kind === 'causes', data: points.map(p => p.label), axisLabel: { color: c.muted, fontSize: 7, width: 126, overflow: 'truncate' }, axisLine: { show: false }, axisTick: { show: false } };
            const total = points.reduce((sum, point) => sum + point.value, 0);
            option.series = [{ type: 'bar', barMaxWidth: kind === 'causes' ? 15 : 11,
                data: points.map((point, index) => kind === 'causes' ? { value: point.value, itemStyle: { color: index < 2 ? c.danger : index === 2 ? '#d97a48' : '#c98718' } } : point.value),
                itemStyle: { color: new echarts.graphic.LinearGradient(0, 0, 1, 0, [{ offset: 0, color: '#7a86ee' }, { offset: 1, color: c.primary }]), borderRadius: [0, 6, 6, 0] },
                label: { show: true, position: 'right', color: c.muted, fontSize: 7, formatter: kind === 'causes' ? p => `${p.value} · ${total ? Math.round(p.value * 100 / total) : 0}%` : undefined } }];
        }
        chart.setOption(option, true);
        return true;
    }
    window.KrackendRuntimeCharts = { replace };
    new MutationObserver(() => lastSeries.forEach((series, id) => replace(id, series)))
        .observe(document.documentElement, { attributes: true, attributeFilter: ['class', 'style', 'data-theme', 'data-bs-theme', 'data-krackend-theme'] });
    window.addEventListener('pagehide', () => { observers.forEach(disconnect => disconnect()); charts.forEach(chart => chart.dispose()); }, { once: true });
})();
