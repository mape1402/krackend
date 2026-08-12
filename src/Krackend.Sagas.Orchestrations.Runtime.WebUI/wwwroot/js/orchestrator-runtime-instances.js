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

    const stageModalElement = document.getElementById("runtimeStageModal");
    const stageTitle = document.querySelector("[data-stage-title]");
    const stageSubtitle = document.querySelector("[data-stage-subtitle]");
    const stageBody = document.querySelector("[data-stage-body]");
    const stageModal = stageModalElement && window.bootstrap ? new bootstrap.Modal(stageModalElement) : null;

    const timelineModalElement = document.getElementById("runtimeTimelineModal");
    const timelineTitle = document.querySelector("[data-timeline-title]");
    const timelineSubtitle = document.querySelector("[data-timeline-subtitle]");
    const timelineBody = document.querySelector("[data-timeline-body]");
    const timelineModal = timelineModalElement && window.bootstrap ? new bootstrap.Modal(timelineModalElement) : null;

    const instances = new Map();
    const traffic = new Map();
    let selectedInstanceId = null;
    let selectedStageId = null;
    let currentDetail = null;
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
        liveState?.setAttribute("data-live-state", state);
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
        if (!value) {
            return "-";
        }

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
        selectedStageId = null;
        currentDetail = null;

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
        detailRefreshTimer = window.setTimeout(() => loadDetail(instanceId, true), 80);
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
        currentDetail = detail;
        const instance = detail.instance;
        const stages = detail.stages || [];
        const previousStageStillExists = stages.some(stage => stage.id === selectedStageId);
        selectedStageId = previousStageStillExists ? selectedStageId : getPreferredStageId(detail);

        if (detailTitle) {
            detailTitle.textContent = instance.orchestrationDefinitionKey;
        }
        if (detailSubtitle) {
            detailSubtitle.textContent = `${instance.id} | ${instance.status} | ${instance.correlationId}`;
        }
        if (!detailBody) {
            return;
        }

        detailBody.innerHTML = `
            <section class="od-trace-shell">
                <div class="od-trace-actions">
                    <div class="od-trace-summary">
                        ${field("Status", badge(instance.status), true)}
                        ${field("Current stage", instance.currentStageKey || "-")}
                        ${field("Current task", instance.currentTaskKey || "-")}
                        ${field("Execution key", instance.executionKey || "-")}
                        ${field("Started", formatDate(instance.startedOnUtc))}
                        ${field("Updated", formatDate(instance.lastUpdatedOnUtc))}
                    </div>
                    <button type="button" class="btn btn-outline-primary od-icon-action" data-open-timeline>
                        Timeline (${(detail.transitions || []).length})
                    </button>
                </div>
                <div class="od-trace-layout">
                    <aside class="od-stage-stepper" aria-label="Stages">
                        ${stages.map(renderStageStep).join("") || `<p class="od-empty">No stages recorded.</p>`}
                    </aside>
                    <section class="od-stepper-hint">
                        <strong>Click a stage to inspect its tasks.</strong>
                        <span>Each stage opens with attempts, dispatch, input and output without crowding this view.</span>
                    </section>
                </div>
            </section>`;
    }

    function getPreferredStageId(detail) {
        const instance = detail.instance;
        const stages = detail.stages || [];
        const current = stages.find(stage => stage.stageKey === instance.currentStageKey);
        const active = stages.find(stage => stage.status === "Running" || stage.status === "Waiting");
        return (current || active || stages[0])?.id || null;
    }

    function renderStageStep(stage) {
        const taskCount = (stage.tasks || []).length;
        const activeClass = stage.id === selectedStageId ? "active" : "";
        const waitingTask = (stage.tasks || []).find(task => task.status === "Waiting" || task.status === "WaitingResponse" || task.waitingSinceUtc);
        return `
            <button type="button" class="od-stage-step ${activeClass}" data-open-stage="${escapeHtml(stage.id)}">
                <span class="od-stage-step-marker">${escapeHtml(stage.order)}</span>
                <span class="od-stage-step-copy">
                    <strong>${escapeHtml(stage.stageKey)}</strong>
                    <small>${taskCount} task${taskCount === 1 ? "" : "s"} | ${formatDate(stage.startedOnUtc)}</small>
                    ${waitingTask ? `<em>Waiting: ${escapeHtml(waitingTask.taskKey)}</em>` : ""}
                </span>
                ${badge(stage.status)}
            </button>`;
    }

    function openStage(stageId) {
        const stage = findStage(stageId);
        if (!stage || !stageBody) {
            return;
        }

        selectedStageId = stage.id;
        detailBody?.querySelectorAll("[data-open-stage]").forEach(step => step.classList.toggle("active", step.getAttribute("data-open-stage") === selectedStageId));
        if (stageTitle) {
            stageTitle.textContent = stage.stageKey;
        }
        if (stageSubtitle) {
            stageSubtitle.textContent = `Stage ${stage.order} | ${stage.status} | ${formatDate(stage.startedOnUtc)} -> ${formatDate(stage.completedOnUtc || stage.failedOnUtc)}`;
        }
        stageBody.innerHTML = renderStageDetail(stage);
        stageModal?.show();
    }

    function renderStageDetail(stage) {
        const tasks = stage.tasks || [];
        return `
            <article class="od-stage-focus-card">
                <header class="od-stage-focus-head">
                    <div>
                        <p class="od-kicker">Stage ${escapeHtml(stage.order)}</p>
                        <h3>${escapeHtml(stage.stageKey)}</h3>
                        <small>${formatDate(stage.startedOnUtc)} -> ${formatDate(stage.completedOnUtc || stage.failedOnUtc)}</small>
                    </div>
                    ${badge(stage.status)}
                </header>
                ${stage.errorSummary ? `<p class="od-runtime-error">${escapeHtml(stage.errorSummary)}</p>` : ""}
                <div class="od-detail-grid od-stage-stats">
                    ${field("Started", formatDate(stage.startedOnUtc))}
                    ${field("Completed", formatDate(stage.completedOnUtc))}
                    ${field("Failed", formatDate(stage.failedOnUtc))}
                    ${field("Tasks", String(tasks.length))}
                </div>
                <div class="od-stage-payload-strip">
                    <details>
                        <summary>Stage metadata</summary>
                        ${codeBlock(stage.metadata)}
                    </details>
                </div>
                <div class="od-task-list">
                    ${tasks.map(renderTaskRow).join("") || `<p class="od-empty">No task executions recorded for this stage.</p>`}
                </div>
            </article>`;
    }

    function renderTaskRow(task) {
        const attempts = task.attempts || [];
        const latestAttempt = attempts[attempts.length - 1];
        const windowText = `${formatDate(task.startedOnUtc)} -> ${formatDate(task.completedOnUtc || task.failedOnUtc || task.waitingSinceUtc)}`;
        return `
            <button type="button" class="od-task-card" data-open-task="${escapeHtml(task.id)}">
                <span class="od-task-main">
                    <strong>${escapeHtml(task.taskKey)}</strong>
                    <small>${escapeHtml(task.taskKind)} | ${escapeHtml(task.executionMode)} | ${windowText}</small>
                    ${task.errorSummary ? `<em>${escapeHtml(task.errorSummary)}</em>` : ""}
                </span>
                <span class="od-task-side">
                    ${badge(task.status)}
                    <small>${attempts.length} attempt${attempts.length === 1 ? "" : "s"}</small>
                    <small>${escapeHtml(latestAttempt?.correlationId || task.correlationId || "-")}</small>
                </span>
            </button>`;
    }

    function openTask(taskId) {
        const task = findTask(taskId);
        const stage = findStageForTask(taskId);
        if (!task || !stage || !stageBody) {
            return;
        }

        selectedStageId = stage.id;
        const attempts = task.attempts || [];
        if (stageTitle) {
            stageTitle.textContent = task.taskKey;
        }
        if (stageSubtitle) {
            stageSubtitle.textContent = `${stage.stageKey} | ${task.status} | ${task.correlationId || "-"}`;
        }
        stageBody.innerHTML = `
            <section class="od-task-detail-shell">
                <button type="button" class="btn btn-outline-secondary od-back-action" data-back-stage="${escapeHtml(stage.id)}">
                    Back to ${escapeHtml(stage.stageKey)}
                </button>
                <div class="od-task-detail-head">
                    <header class="od-stage-focus-head">
                        <div>
                            <p class="od-kicker">${escapeHtml(stage.stageKey)}</p>
                            <h3>${escapeHtml(task.taskKey)}</h3>
                            <small>${escapeHtml(task.correlationId || "-")}</small>
                        </div>
                        ${badge(task.status)}
                    </header>
                    <div class="od-detail-grid">
                        ${field("Status", badge(task.status), true)}
                        ${field("Kind", task.taskKind || "-")}
                        ${field("Mode", task.executionMode || "-")}
                        ${field("Await response", task.awaitResponse ? "yes" : "no")}
                        ${field("Started", formatDate(task.startedOnUtc))}
                        ${field("Waiting", formatDate(task.waitingSinceUtc))}
                        ${field("Completed", formatDate(task.completedOnUtc))}
                        ${field("Failed", formatDate(task.failedOnUtc))}
                        ${field("Last attempt", String(task.lastAttemptNumber))}
                    </div>
                </div>
                ${task.errorSummary ? `<p class="od-runtime-error">${escapeHtml(task.errorSummary)}</p>` : ""}
                <section class="od-detail-split">
                    <article>
                        <h5>Task output variables</h5>
                        ${codeBlock(task.outputVariablesPayload)}
                    </article>
                    <article>
                        <h5>Task metadata</h5>
                        ${codeBlock(task.metadata)}
                    </article>
                </section>
                <section class="od-attempt-stack">
                    <h3>Attempts</h3>
                    ${attempts.map(renderAttemptDetail).join("") || `<p class="od-empty">No attempts recorded.</p>`}
                </section>
            </section>`;
        stageModal?.show();
    }

    function renderAttemptDetail(attempt) {
        return `
            <article class="od-attempt-card">
                <header>
                    <div>
                        <strong>Attempt ${attempt.attemptNumber}</strong>
                        <small>${formatDate(attempt.startedOnUtc)} -> ${formatDate(attempt.completedOnUtc || attempt.failedOnUtc || attempt.waitingSinceUtc)}</small>
                    </div>
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

    function openTimeline() {
        if (!currentDetail || !timelineBody) {
            return;
        }

        const instance = currentDetail.instance;
        const transitions = currentDetail.transitions || [];
        if (timelineTitle) {
            timelineTitle.textContent = "Execution timeline";
        }
        if (timelineSubtitle) {
            timelineSubtitle.textContent = `${instance.id} | ${instance.status}`;
        }

        timelineBody.innerHTML = `
            <section class="od-timeline-control">
                ${transitions.map(renderTimelineEntry).join("") || `<p class="od-empty">No transitions recorded.</p>`}
            </section>`;
        timelineModal?.show();
    }

    function renderTimelineEntry(transition, index) {
        const context = describeTransition(transition);
        return `
            <article class="od-timeline-entry">
                <div class="od-timeline-marker">
                    <span>${index + 1}</span>
                </div>
                <div class="od-timeline-content">
                    <header>
                        <div>
                            <strong>${escapeHtml(transition.transitionType)}</strong>
                            <small>${escapeHtml(context.summary)}</small>
                        </div>
                        <time>${formatDate(transition.occurredOnUtc)}</time>
                    </header>
                    <div class="od-transition-status">${escapeHtml(transition.fromStatus || "-")} -> ${escapeHtml(transition.toStatus || "-")}</div>
                    ${shouldRenderTransitionMessage(transition, context) ? `<p>${escapeHtml(transition.message)}</p>` : ""}
                    ${context.chips.length ? `<div class="od-chip-row">${context.chips.map(chip => `<span class="od-meta-chip">${escapeHtml(chip)}</span>`).join("")}</div>` : ""}
                    ${transition.payload ? `<details><summary>Payload</summary>${codeBlock(transition.payload)}</details>` : ""}
                </div>
            </article>`;
    }

    function describeTransition(transition) {
        const stage = transition.stageExecutionId ? findStage(transition.stageExecutionId) : findStageForTask(transition.taskExecutionId);
        const inferredTask = findTaskForAttempt(transition.taskExecutionAttemptId) || findPreviousTaskForTransition(transition);
        const task = transition.taskExecutionId ? findTask(transition.taskExecutionId) : inferredTask;
        const attempt = transition.taskExecutionAttemptId ? findAttempt(transition.taskExecutionAttemptId) : null;
        const dispatch = attempt?.dispatch;
        const chips = [];

        if (stage) {
            chips.push(`Stage: ${stage.stageKey}`);
        }
        if (task) {
            chips.push(`Task: ${task.taskKey}`);
        }
        if (attempt) {
            chips.push(`Attempt: ${attempt.attemptNumber}`);
        }
        if (dispatch?.destination) {
            chips.push(`Destination: ${dispatch.destination}`);
        }

        let summary = transition.message || transition.transitionType;
        if (transition.transitionType?.startsWith("Stage") && stage) {
            summary = `${stage.stageKey} | ${transition.transitionType}`;
        } else if (transition.transitionType?.startsWith("Task") && task) {
            summary = `${task.taskKey} | ${transition.transitionType}`;
        } else if (transition.transitionType === "InstanceWaitingResponse" && task) {
            summary = `Instance waiting for ${task.taskKey}`;
        } else if (transition.transitionType === "InstanceStarted") {
            summary = `Instance started: ${currentDetail?.instance?.orchestrationDefinitionKey || "-"}`;
        }

        return { summary, chips };
    }

    function shouldRenderTransitionMessage(transition, context) {
        if (!transition.message) {
            return false;
        }

        return transition.message !== transition.transitionType && transition.message !== context.summary;
    }

    function findStage(stageId) {
        return (currentDetail?.stages || []).find(stage => stage.id === stageId);
    }

    function findTask(taskId) {
        if (!taskId) {
            return null;
        }

        for (const stage of currentDetail?.stages || []) {
            const task = (stage.tasks || []).find(item => item.id === taskId);
            if (task) {
                return task;
            }
        }

        return null;
    }

    function findTaskForAttempt(attemptId) {
        if (!attemptId) {
            return null;
        }

        for (const stage of currentDetail?.stages || []) {
            const task = (stage.tasks || []).find(item => (item.attempts || []).some(attempt => attempt.id === attemptId));
            if (task) {
                return task;
            }
        }

        return null;
    }

    function findAttempt(attemptId) {
        const task = findTaskForAttempt(attemptId);
        return (task?.attempts || []).find(attempt => attempt.id === attemptId) || null;
    }

    function findPreviousTaskForTransition(transition) {
        const transitions = currentDetail?.transitions || [];
        const index = transitions.findIndex(item => item.id === transition.id);
        if (index < 1) {
            return null;
        }

        for (let i = index - 1; i >= 0; i--) {
            const candidate = transitions[i];
            const task = candidate.taskExecutionId ? findTask(candidate.taskExecutionId) : findTaskForAttempt(candidate.taskExecutionAttemptId);
            if (task) {
                return task;
            }
        }

        return null;
    }

    function findStageForTask(taskId) {
        if (!taskId) {
            return null;
        }

        return (currentDetail?.stages || []).find(stage => (stage.tasks || []).some(task => task.id === taskId));
    }

    function field(label, value, allowHtml) {
        const content = allowHtml ? value : escapeHtml(value);
        return `<div class="od-detail-field"><span>${escapeHtml(label)}</span><strong>${content}</strong></div>`;
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

        const stageStep = event.target.closest("[data-open-stage]");
        if (stageStep) {
            openStage(stageStep.getAttribute("data-open-stage"));
            return;
        }

        const taskTrigger = event.target.closest("[data-open-task]");
        if (taskTrigger) {
            openTask(taskTrigger.getAttribute("data-open-task"));
            return;
        }

        const backToStage = event.target.closest("[data-back-stage]");
        if (backToStage) {
            selectedStageId = backToStage.getAttribute("data-back-stage");
            const stage = findStage(selectedStageId);
            if (stage && stageBody) {
                if (stageTitle) {
                    stageTitle.textContent = stage.stageKey;
                }
                if (stageSubtitle) {
                    stageSubtitle.textContent = `Stage ${stage.order} | ${stage.status} | ${formatDate(stage.startedOnUtc)} -> ${formatDate(stage.completedOnUtc || stage.failedOnUtc)}`;
                }
                stageBody.innerHTML = renderStageDetail(stage);
            }
            return;
        }

        const timelineTrigger = event.target.closest("[data-open-timeline]");
        if (timelineTrigger) {
            openTimeline();
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
