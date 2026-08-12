(function () {
    const config = window.KrackendRuntimeDashboard;
    if (!config || !window.signalR) {
        return;
    }

    const liveState = document.querySelector("[data-live-state]");
    const liveLabel = document.querySelector("[data-live-label]");
    const grid = document.querySelector("[data-instance-grid]");
    const instanceCount = document.querySelector("[data-instance-count]");
    const chartCanvas = document.querySelector("[data-traffic-chart]");
    const detailModalElement = document.getElementById("runtimeInstanceModal");
    const detailTitle = document.querySelector("[data-detail-title]");
    const detailSubtitle = document.querySelector("[data-detail-subtitle]");
    const detailBody = document.querySelector("[data-detail-body]");
    const detailModal = detailModalElement && window.bootstrap ? new bootstrap.Modal(detailModalElement) : null;

    const instances = new Map();
    const traffic = new Map();
    let selectedInstanceId = null;
    let detailRefreshTimer = null;

    (config.instances || []).forEach(item => instances.set(item.id, normalizeRow(item)));
    (config.traffic || []).forEach(item => {
        const bucket = bucketKey(item.bucketUtc);
        traffic.set(bucket, (traffic.get(bucket) || 0) + item.count);
    });

    renderCounters();
    drawTraffic();

    function normalizeRow(item) {
        return {
            id: item.id,
            orchestrationDefinitionKey: item.orchestrationDefinitionKey || "",
            correlationId: item.correlationId || "",
            executionKey: item.executionKey || "",
            status: item.status || "",
            statusClass: item.statusClass || statusClass(item.status),
            currentStageKey: item.currentStageKey || "",
            currentTaskKey: item.currentTaskKey || "",
            startedOnUtc: item.startedOnUtc,
            lastUpdatedOnUtc: item.lastUpdatedOnUtc,
            waitingSinceUtc: item.waitingSinceUtc,
            completedOnUtc: item.completedOnUtc,
            failedOnUtc: item.failedOnUtc,
            errorSummary: item.errorSummary || ""
        };
    }

    function setLiveState(state, label) {
        if (liveState) {
            liveState.setAttribute("data-live-state", state);
        }

        if (liveLabel) {
            liveLabel.textContent = label;
        }
    }

    function bucketKey(value) {
        const date = new Date(value);
        if (Number.isNaN(date.getTime())) {
            return "";
        }

        date.setSeconds(0, 0);
        return date.toISOString();
    }

    function formatDate(value) {
        const date = new Date(value);
        if (Number.isNaN(date.getTime())) {
            return "-";
        }

        return date.toISOString().replace("T", " ").substring(0, 19);
    }

    function renderCounters() {
        const rows = Array.from(instances.values());
        setCounter("active", rows.filter(row => row.status === "Running" || row.status === "Waiting").length);
        setCounter("waiting", rows.filter(row => row.status === "Waiting").length);
        setCounter("completed", rows.filter(row => row.status === "Completed").length);
        setCounter("failed", rows.filter(row => row.status === "Failed").length);
        if (instanceCount) {
            instanceCount.textContent = rows.length;
        }
    }

    function setCounter(name, value) {
        const element = document.querySelector(`[data-counter="${name}"]`);
        if (element) {
            element.textContent = value;
        }
    }

    function drawTraffic() {
        if (!chartCanvas) {
            return;
        }

        const context = chartCanvas.getContext("2d");
        const width = chartCanvas.clientWidth || chartCanvas.width;
        const height = chartCanvas.height;
        const ratio = window.devicePixelRatio || 1;
        chartCanvas.width = width * ratio;
        chartCanvas.height = height * ratio;
        context.setTransform(ratio, 0, 0, ratio, 0, 0);
        context.clearRect(0, 0, width, height);

        const points = Array.from(traffic.entries()).sort((a, b) => a[0].localeCompare(b[0])).slice(-24);
        const max = Math.max(1, ...points.map(x => x[1]));
        const padding = { top: 16, right: 18, bottom: 28, left: 34 };
        const plotWidth = Math.max(1, width - padding.left - padding.right);
        const plotHeight = Math.max(1, height - padding.top - padding.bottom);

        context.strokeStyle = "#d9deee";
        context.lineWidth = 1;
        for (let i = 0; i <= 3; i++) {
            const y = padding.top + (plotHeight / 3) * i;
            context.beginPath();
            context.moveTo(padding.left, y);
            context.lineTo(width - padding.right, y);
            context.stroke();
        }

        if (points.length === 0) {
            context.fillStyle = "#5f6687";
            context.font = "13px system-ui";
            context.fillText("No traffic yet", padding.left, padding.top + 20);
            return;
        }

        context.strokeStyle = "#4856d7";
        context.lineWidth = 2.5;
        context.beginPath();
        points.forEach((point, index) => {
            const x = padding.left + (points.length === 1 ? plotWidth : (plotWidth / (points.length - 1)) * index);
            const y = padding.top + plotHeight - (point[1] / max) * plotHeight;
            if (index === 0) {
                context.moveTo(x, y);
            } else {
                context.lineTo(x, y);
            }
        });
        context.stroke();

        context.fillStyle = "#0f8c55";
        points.forEach((point, index) => {
            const x = padding.left + (points.length === 1 ? plotWidth : (plotWidth / (points.length - 1)) * index);
            const y = padding.top + plotHeight - (point[1] / max) * plotHeight;
            context.beginPath();
            context.arc(x, y, 3.5, 0, Math.PI * 2);
            context.fill();
        });

        context.fillStyle = "#5f6687";
        context.font = "11px system-ui";
        context.fillText(String(max), 6, padding.top + 4);
        context.fillText("0", 18, padding.top + plotHeight + 4);
        context.fillText("now", width - padding.right - 20, height - 8);
    }

    function applyEvent(eventData) {
        const id = eventData.orchestrationInstanceId;
        const existing = instances.get(id);
        const row = normalizeRow({
            id,
            orchestrationDefinitionKey: eventData.orchestrationDefinitionKey || existing?.orchestrationDefinitionKey,
            correlationId: eventData.correlationId || existing?.correlationId,
            executionKey: eventData.executionKey || existing?.executionKey,
            status: eventData.instanceStatus || eventData.toStatus || existing?.status,
            currentStageKey: eventData.stageKey || existing?.currentStageKey,
            currentTaskKey: eventData.taskKey || existing?.currentTaskKey,
            startedOnUtc: existing?.startedOnUtc || eventData.occurredOnUtc,
            lastUpdatedOnUtc: eventData.occurredOnUtc,
            waitingSinceUtc: existing?.waitingSinceUtc,
            completedOnUtc: eventData.instanceStatus === "Completed" ? eventData.occurredOnUtc : existing?.completedOnUtc,
            failedOnUtc: eventData.instanceStatus === "Failed" ? eventData.occurredOnUtc : existing?.failedOnUtc,
            errorSummary: existing?.errorSummary
        });

        instances.set(id, row);
        upsertGridRow(row, true);

        const bucket = bucketKey(eventData.occurredOnUtc);
        traffic.set(bucket, (traffic.get(bucket) || 0) + 1);

        renderCounters();
        drawTraffic();

        if (selectedInstanceId === id) {
            scheduleDetailRefresh(id);
        }
    }

    function upsertGridRow(row, moveToTop) {
        if (!grid) {
            return;
        }

        let element = grid.querySelector(`[data-instance-id="${row.id}"]`);
        if (!element) {
            element = document.createElement("tr");
            element.setAttribute("data-instance-id", row.id);
            element.setAttribute("tabindex", "0");
            grid.prepend(element);
        } else if (moveToTop) {
            grid.prepend(element);
        }

        element.setAttribute("data-instance-status", row.status);
        element.classList.add("live");
        element.innerHTML = `
            <td>
                <strong>${escapeHtml(row.orchestrationDefinitionKey)}</strong>
                <small>${escapeHtml(row.correlationId)}</small>
                <small>${escapeHtml(row.id)}</small>
            </td>
            <td><span class="od-status ${statusClass(row.status)}" data-instance-status-label>${escapeHtml(row.status)}</span></td>
            <td data-instance-stage>${escapeHtml(row.currentStageKey || "-")}</td>
            <td data-instance-task>${escapeHtml(row.currentTaskKey || "-")}</td>
            <td>${formatDate(row.startedOnUtc)}</td>
            <td data-instance-updated>${formatDate(row.lastUpdatedOnUtc)}</td>`;
    }

    async function openDetail(instanceId) {
        selectedInstanceId = instanceId;
        if (detailTitle) {
            detailTitle.textContent = "Loading trace";
        }
        if (detailSubtitle) {
            detailSubtitle.textContent = instanceId;
        }
        if (detailBody) {
            detailBody.innerHTML = `<p class="od-empty">Loading full execution trace...</p>`;
        }
        detailModal?.show();
        await loadDetail(instanceId);
    }

    function scheduleDetailRefresh(instanceId) {
        window.clearTimeout(detailRefreshTimer);
        detailRefreshTimer = window.setTimeout(() => loadDetail(instanceId, true), 150);
    }

    async function loadDetail(instanceId, silent) {
        try {
            const separator = config.detailPath.includes("?") ? "&" : "?";
            const response = await fetch(`${config.detailPath}${separator}instanceId=${encodeURIComponent(instanceId)}`, {
                headers: { "Accept": "application/json" }
            });
            if (!response.ok) {
                throw new Error(`Detail request failed: ${response.status}`);
            }
            renderDetail(await response.json());
        } catch (error) {
            if (!silent && detailBody) {
                detailBody.innerHTML = `<p class="od-runtime-error">${escapeHtml(error.message)}</p>`;
            }
        }
    }

    function renderDetail(detail) {
        const instance = detail.instance;
        if (detailTitle) {
            detailTitle.textContent = instance.orchestrationDefinitionKey;
        }
        if (detailSubtitle) {
            detailSubtitle.textContent = `${instance.id} · ${instance.status} · ${instance.correlationId}`;
        }
        if (!detailBody) {
            return;
        }

        detailBody.innerHTML = `
            <section class="od-detail-summary">
                ${field("Status", badge(instance.status))}
                ${field("Current stage", instance.currentStageKey || "-")}
                ${field("Current task", instance.currentTaskKey || "-")}
                ${field("Execution key", instance.executionKey || "-")}
                ${field("Started", formatDate(instance.startedOnUtc))}
                ${field("Updated", formatDate(instance.lastUpdatedOnUtc))}
            </section>
            <section class="od-detail-split">
                <article>
                    <h3>Snapshot payload</h3>
                    ${codeBlock(detail.snapshotPayload)}
                </article>
                <article>
                    <h3>Instance metadata</h3>
                    ${codeBlock(detail.metadata)}
                </article>
            </section>
            <section class="od-detail-stages">
                ${detail.stages.map(renderStage).join("")}
            </section>
            <section class="od-collapse-block">
                <button type="button" class="od-collapse-toggle" data-collapse-target="runtimeTimeline">
                    Timeline (${detail.transitions.length})
                </button>
                <div id="runtimeTimeline" class="od-collapse-content" hidden>
                    ${detail.transitions.map(renderTransition).join("")}
                </div>
            </section>`;
    }

    function renderStage(stage) {
        return `
            <article class="od-detail-stage">
                <header>
                    <div>
                        <h3>${escapeHtml(stage.stageKey)}</h3>
                        <small>Order ${stage.order} · ${formatDate(stage.startedOnUtc)} -> ${formatDate(stage.completedOnUtc || stage.failedOnUtc)}</small>
                    </div>
                    ${badge(stage.status)}
                </header>
                ${stage.errorSummary ? `<p class="od-runtime-error">${escapeHtml(stage.errorSummary)}</p>` : ""}
                <div class="od-detail-task-list">
                    ${stage.tasks.map(renderTask).join("") || `<p class="od-empty">No task executions recorded.</p>`}
                </div>
            </article>`;
    }

    function renderTask(task) {
        return `
            <article class="od-detail-task">
                <header>
                    <div>
                        <h4>${escapeHtml(task.taskKey)}</h4>
                        <small>${escapeHtml(task.taskKind)} · ${escapeHtml(task.executionMode)} · correlation ${escapeHtml(task.correlationId || "-")}</small>
                    </div>
                    ${badge(task.status)}
                </header>
                <div class="od-detail-grid">
                    ${field("Await response", task.awaitResponse ? "yes" : "no")}
                    ${field("Started", formatDate(task.startedOnUtc))}
                    ${field("Waiting", formatDate(task.waitingSinceUtc))}
                    ${field("Completed", formatDate(task.completedOnUtc))}
                    ${field("Failed", formatDate(task.failedOnUtc))}
                    ${field("Last attempt", String(task.lastAttemptNumber))}
                </div>
                <div class="od-detail-split">
                    <article>
                        <h5>Task output variables</h5>
                        ${codeBlock(task.outputVariablesPayload)}
                    </article>
                    <article>
                        <h5>Task metadata</h5>
                        ${codeBlock(task.metadata)}
                    </article>
                </div>
                <div class="od-attempts">
                    ${task.attempts.map(renderAttempt).join("") || `<p class="od-empty">No attempts recorded.</p>`}
                </div>
            </article>`;
    }

    function renderAttempt(attempt) {
        return `
            <article class="od-attempt">
                <header>
                    <strong>Attempt ${attempt.attemptNumber}</strong>
                    ${badge(attempt.status)}
                </header>
                <div class="od-detail-grid">
                    ${field("Started", formatDate(attempt.startedOnUtc))}
                    ${field("Waiting", formatDate(attempt.waitingSinceUtc))}
                    ${field("Completed", formatDate(attempt.completedOnUtc))}
                    ${field("Failed", formatDate(attempt.failedOnUtc))}
                    ${field("Timed out", formatDate(attempt.timedOutOnUtc))}
                    ${field("Error", attempt.errorMessage || attempt.errorCode || "-")}
                </div>
                <div class="od-detail-split">
                    <article>
                        <h5>Input</h5>
                        ${codeBlock(attempt.requestPayload)}
                    </article>
                    <article>
                        <h5>Output</h5>
                        ${codeBlock(attempt.responsePayload)}
                    </article>
                </div>
                ${attempt.dispatch ? renderDispatch(attempt.dispatch) : ""}
            </article>`;
    }

    function renderDispatch(dispatch) {
        return `
            <section class="od-dispatch">
                <h5>Dispatch</h5>
                <div class="od-detail-grid">
                    ${field("Type", dispatch.dispatchType)}
                    ${field("Destination", dispatch.destination)}
                    ${field("Status", dispatch.dispatchStatus)}
                    ${field("Command", dispatch.commandId)}
                    ${field("Correlation", dispatch.correlationId)}
                    ${field("Sent", formatDate(dispatch.sentOnUtc))}
                    ${field("Ack", formatDate(dispatch.acknowledgedOnUtc))}
                    ${field("Failure", dispatch.failureReason || "-")}
                </div>
                <div class="od-detail-split">
                    <article>
                        <h5>Dispatch payload</h5>
                        ${codeBlock(dispatch.requestPayload)}
                    </article>
                    <article>
                        <h5>Dispatch metadata</h5>
                        ${codeBlock(dispatch.metadata)}
                    </article>
                </div>
            </section>`;
    }

    function renderTransition(transition) {
        return `
            <article class="od-timeline-item">
                <div>
                    <strong>${escapeHtml(transition.transitionType)}</strong>
                    <span>${escapeHtml(transition.fromStatus || "")} -> ${escapeHtml(transition.toStatus || "")}</span>
                    <span>${escapeHtml(transition.message || "")}</span>
                </div>
                <time>${formatDate(transition.occurredOnUtc)}</time>
                ${transition.payload ? codeBlock(transition.payload) : ""}
            </article>`;
    }

    function field(label, value) {
        return `<div class="od-detail-field"><span>${escapeHtml(label)}</span><strong>${typeof value === "string" ? escapeHtml(value) : value}</strong></div>`;
    }

    function badge(status) {
        return `<span class="od-status ${statusClass(status)}">${escapeHtml(status || "-")}</span>`;
    }

    function codeBlock(value) {
        return `<pre class="od-json">${escapeHtml(value || "{}")}</pre>`;
    }

    function statusClass(status) {
        switch (status) {
            case "Running":
                return "od-status-running";
            case "Waiting":
                return "od-status-waiting";
            case "Completed":
                return "od-status-active";
            case "Failed":
                return "od-status-danger";
            default:
                return "od-status-inactive";
        }
    }

    function escapeHtml(value) {
        return String(value ?? "")
            .replaceAll("&", "&amp;")
            .replaceAll("<", "&lt;")
            .replaceAll(">", "&gt;")
            .replaceAll("\"", "&quot;")
            .replaceAll("'", "&#039;");
    }

    document.addEventListener("click", function (event) {
        const row = event.target.closest("[data-instance-id]");
        if (row && row.closest("[data-instance-grid]")) {
            openDetail(row.getAttribute("data-instance-id"));
            return;
        }

        const toggle = event.target.closest("[data-collapse-target]");
        if (toggle) {
            const content = document.getElementById(toggle.getAttribute("data-collapse-target"));
            if (content) {
                content.hidden = !content.hidden;
            }
        }
    });

    document.addEventListener("keydown", function (event) {
        if (event.key !== "Enter") {
            return;
        }

        const row = event.target.closest("[data-instance-id]");
        if (row && row.closest("[data-instance-grid]")) {
            openDetail(row.getAttribute("data-instance-id"));
        }
    });

    window.addEventListener("resize", drawTraffic);

    const connection = new signalR.HubConnectionBuilder()
        .withUrl(config.hubPath)
        .withAutomaticReconnect()
        .build();

    connection.on("runtime.transition", applyEvent);

    connection.onreconnecting(function () {
        setLiveState("connecting", "Reconnecting");
    });

    connection.onreconnected(async function () {
        setLiveState("connected", "Live");
        await connection.invoke("WatchEnvironment", config.environmentKey);
        if (selectedInstanceId) {
            await connection.invoke("WatchInstance", selectedInstanceId);
        }
    });

    connection.start()
        .then(async function () {
            setLiveState("connected", "Live");
            await connection.invoke("WatchEnvironment", config.environmentKey);
        })
        .catch(function () {
            setLiveState("disconnected", "Offline");
        });
})();
