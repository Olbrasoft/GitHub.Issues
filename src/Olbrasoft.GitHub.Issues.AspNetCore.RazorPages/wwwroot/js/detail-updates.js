// SignalR client for progressive summary loading on issue detail page
(function () {
    'use strict';

    let connection = null;
    let issueId = null;

    // Initialize when DOM is ready
    document.addEventListener('DOMContentLoaded', function () {
        initializeDetailUpdates();
    });

    function initializeDetailUpdates() {
        const container = document.getElementById('ai-summary-container');
        if (!container) {
            return;
        }

        issueId = parseInt(container.dataset.issueId, 10);
        const summaryPending = container.dataset.summaryPending === 'true';

        if (!issueId || isNaN(issueId)) {
            return;
        }

        // Only connect to SignalR if summary is pending
        if (summaryPending) {
            initializeSignalR();
        }
    }

    function initializeSignalR() {
        // Create SignalR connection
        connection = new signalR.HubConnectionBuilder()
            .withUrl('/hubs/issues')
            .withAutomaticReconnect([0, 2000, 5000, 10000, 30000])
            .configureLogging(signalR.LogLevel.Warning)
            .build();

        // Handle incoming summary
        connection.on('SummaryReceived', handleSummaryReceived);

        // Handle connection state changes
        connection.onreconnecting(function (error) {
            console.log('SignalR reconnecting...', error);
        });

        connection.onreconnected(function (connectionId) {
            console.log('SignalR reconnected:', connectionId);
            subscribeToIssue();
        });

        connection.onclose(function (error) {
            console.log('SignalR connection closed:', error);
        });

        // Start connection
        startConnection();
    }

    async function startConnection() {
        try {
            await connection.start();
            console.log('SignalR connected for detail page');
            await subscribeToIssue();
            // Trigger summary generation after subscribing
            triggerSummaryGeneration();
        } catch (err) {
            console.error('SignalR connection error:', err);
            // Retry connection after 5 seconds
            setTimeout(startConnection, 5000);
        }
    }

    async function subscribeToIssue() {
        if (!issueId) {
            return;
        }

        try {
            await connection.invoke('SubscribeToIssues', [issueId]);
            console.log('Subscribed to issue', issueId);
        } catch (err) {
            console.error('Failed to subscribe to issue:', err);
        }
    }

    function triggerSummaryGeneration() {
        if (!issueId) {
            return;
        }

        // Fire-and-forget API call to trigger summary generation
        fetch(`/api/issues/${issueId}/generate-summary`, {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json'
            }
        })
        .then(response => {
            if (response.ok) {
                console.log('Summary generation triggered for issue', issueId);
            } else {
                console.error('Failed to trigger summary generation:', response.status);
            }
        })
        .catch(err => {
            console.error('Error triggering summary generation:', err);
        });
    }

    function handleSummaryReceived(data) {
        console.log('Received summary for issue:', data);

        if (data.issueId !== issueId) {
            return;
        }

        const container = document.getElementById('ai-summary-container');
        if (!container) {
            return;
        }

        // Replace loading indicator with actual summary
        container.innerHTML = `
            <div class="ai-summary">
                <div class="ai-summary-header">
                    <span class="ai-icon">🤖</span>
                    <span class="ai-label">AI Shrnutí</span>
                    <span class="ai-provider">${escapeHtml(data.provider)}</span>
                </div>
                <p class="ai-summary-text">${escapeHtml(data.summary)}</p>
            </div>
        `;

        // Add highlight animation
        container.querySelector('.ai-summary').classList.add('ai-summary-received');
        setTimeout(function () {
            container.querySelector('.ai-summary')?.classList.remove('ai-summary-received');
        }, 2000);

        // Disconnect SignalR - no longer needed
        if (connection) {
            connection.stop();
        }
    }

    function escapeHtml(text) {
        const div = document.createElement('div');
        div.textContent = text;
        return div.innerHTML;
    }

    // Cleanup on page unload
    window.addEventListener('beforeunload', function () {
        if (connection && connection.state === signalR.HubConnectionState.Connected) {
            connection.stop();
        }
    });
})();
