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
            return;
        }

        row.setAttribute("data-instance-status", eventData.instanceStatus || eventData.toStatus || "");
        const label = row.querySelector("[data-instance-status-label]");
        if (label) {
            label.textContent = eventData.instanceStatus || eventData.toStatus || "";
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
