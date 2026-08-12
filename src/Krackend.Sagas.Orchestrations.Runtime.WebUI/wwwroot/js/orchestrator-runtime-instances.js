(function () {
    const config = window.KrackendRuntimeDashboard;
    if (!config || !window.signalR) {
        return;
    }

    const liveState = document.querySelector("[data-live-state]");
    const liveLabel = document.querySelector("[data-live-label]");
    const timeline = document.querySelector("[data-timeline]");

    function setLiveState(state, label) {
        if (liveState) {
            liveState.setAttribute("data-live-state", state);
        }

        if (liveLabel) {
            liveLabel.textContent = label;
        }
    }

    function formatDate(value) {
        const date = new Date(value);
        if (Number.isNaN(date.getTime())) {
            return "";
        }

        return date.toISOString().replace("T", " ").substring(0, 19);
    }

    function prependTimeline(eventData) {
        if (!timeline || eventData.orchestrationInstanceId !== config.selectedInstanceId) {
            return;
        }

        const item = document.createElement("article");
        item.className = "od-timeline-item live";
        item.innerHTML = `
            <div>
                <strong>${eventData.transitionType}</strong>
                <span>${eventData.fromStatus || ""} -> ${eventData.toStatus || ""}</span>
            </div>
            <time>${formatDate(eventData.occurredOnUtc)}</time>`;
        timeline.prepend(item);
    }

    function updateSelected(eventData) {
        if (eventData.orchestrationInstanceId !== config.selectedInstanceId) {
            return;
        }

        const status = document.querySelector("[data-selected-status]");
        const stage = document.querySelector("[data-selected-stage]");
        const task = document.querySelector("[data-selected-task]");
        const updated = document.querySelector("[data-selected-updated]");

        if (status) {
            status.textContent = eventData.instanceStatus || eventData.toStatus || "";
        }

        if (stage && eventData.stageKey) {
            stage.textContent = eventData.stageKey;
        }

        if (task && eventData.taskKey) {
            task.textContent = eventData.taskKey;
        }

        if (updated) {
            updated.textContent = formatDate(eventData.occurredOnUtc);
        }
    }

    function updateInstanceRow(eventData) {
        const row = document.querySelector(`[data-instance-id="${eventData.orchestrationInstanceId}"]`);
        if (!row) {
            addInstanceRow(eventData);
            updateCounters();
            return;
        }

        row.setAttribute("data-instance-status", eventData.instanceStatus || eventData.toStatus || "");
        const label = row.querySelector("[data-instance-status-label]");
        if (label) {
            label.textContent = eventData.instanceStatus || eventData.toStatus || "";
        }

        updateCounters();
    }

    function addInstanceRow(eventData) {
        const list = document.querySelector(".od-instance-list");
        if (!list) {
            return;
        }

        const id = eventData.orchestrationInstanceId;
        const status = eventData.instanceStatus || eventData.toStatus || "";
        const row = document.createElement("a");
        row.className = "od-instance-row live";
        row.href = `/runtime/instances?instanceId=${encodeURIComponent(id)}`;
        row.setAttribute("data-instance-id", id);
        row.setAttribute("data-instance-status", status);
        row.innerHTML = `
            <span>
                <strong>${eventData.orchestrationDefinitionKey || ""}</strong>
                <small>${eventData.correlationId || ""}</small>
            </span>
            <span class="od-status ${statusClass(status)}" data-instance-status-label>${status}</span>`;
        list.prepend(row);
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

    function updateCounters() {
        const rows = Array.from(document.querySelectorAll("[data-instance-id]"));
        const active = rows.filter(isActive).length;
        const waiting = rows.filter(row => row.getAttribute("data-instance-status") === "Waiting").length;
        const completed = rows.filter(row => row.getAttribute("data-instance-status") === "Completed").length;
        const failed = rows.filter(row => row.getAttribute("data-instance-status") === "Failed").length;
        setCounter("active", active);
        setCounter("waiting", waiting);
        setCounter("completed", completed);
        setCounter("failed", failed);
    }

    function isActive(row) {
        const status = row.getAttribute("data-instance-status");
        return status === "Running" || status === "Waiting";
    }

    function setCounter(name, value) {
        const element = document.querySelector(`[data-counter="${name}"]`);
        if (element) {
            element.textContent = value;
        }
    }

    const connection = new signalR.HubConnectionBuilder()
        .withUrl(config.hubPath)
        .withAutomaticReconnect()
        .build();

    connection.on("runtime.transition", function (eventData) {
        updateInstanceRow(eventData);
        updateSelected(eventData);
        prependTimeline(eventData);
    });

    connection.onreconnecting(function () {
        setLiveState("connecting", "Reconnecting");
    });

    connection.onreconnected(async function () {
        setLiveState("connected", "Live");
        await connection.invoke("WatchEnvironment", config.environmentKey);
        if (config.selectedInstanceId) {
            await connection.invoke("WatchInstance", config.selectedInstanceId);
        }
    });

    connection.start()
        .then(async function () {
            setLiveState("connected", "Live");
            await connection.invoke("WatchEnvironment", config.environmentKey);
            if (config.selectedInstanceId) {
                await connection.invoke("WatchInstance", config.selectedInstanceId);
            }
        })
        .catch(function () {
            setLiveState("disconnected", "Offline");
        });
})();
