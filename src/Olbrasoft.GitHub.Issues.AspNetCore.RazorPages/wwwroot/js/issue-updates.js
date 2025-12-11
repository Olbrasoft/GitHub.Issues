// SignalR client for real-time issue updates
(function () {
    'use strict';

    let connection = null;
    let subscribedIssueIds = [];

    // Initialize SignalR connection when DOM is ready
    document.addEventListener('DOMContentLoaded', function () {
        initializeSignalR();
    });

    function initializeSignalR() {
        // Only initialize if there are issues on the page
        const issueItems = document.querySelectorAll('.result-item[data-issue-id]');
        if (issueItems.length === 0) {
            return;
        }

        // Create SignalR connection
        connection = new signalR.HubConnectionBuilder()
            .withUrl('/hubs/issues')
            .withAutomaticReconnect([0, 2000, 5000, 10000, 30000])
            .configureLogging(signalR.LogLevel.Warning)
            .build();

        // Handle incoming issue updates
        connection.on('IssueUpdated', handleIssueUpdate);

        // Handle connection state changes
        connection.onreconnecting(function (error) {
            console.log('SignalR reconnecting...', error);
        });

        connection.onreconnected(function (connectionId) {
            console.log('SignalR reconnected:', connectionId);
            // Re-subscribe to issues after reconnection
            subscribeToVisibleIssues();
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
            console.log('SignalR connected');
            subscribeToVisibleIssues();
        } catch (err) {
            console.error('SignalR connection error:', err);
            // Retry connection after 5 seconds
            setTimeout(startConnection, 5000);
        }
    }

    async function subscribeToVisibleIssues() {
        const issueItems = document.querySelectorAll('.result-item[data-issue-id]');
        const issueIds = Array.from(issueItems)
            .map(item => parseInt(item.dataset.issueId, 10))
            .filter(id => !isNaN(id));

        if (issueIds.length === 0) {
            return;
        }

        try {
            // Unsubscribe from previous issues if any
            if (subscribedIssueIds.length > 0) {
                await connection.invoke('UnsubscribeFromIssues', subscribedIssueIds);
            }

            // Subscribe to current issues
            await connection.invoke('SubscribeToIssues', issueIds);
            subscribedIssueIds = issueIds;
            console.log('Subscribed to', issueIds.length, 'issues');
        } catch (err) {
            console.error('Failed to subscribe to issues:', err);
        }
    }

    function handleIssueUpdate(update) {
        console.log('Received issue update:', update);

        const issueItem = document.querySelector(`.result-item[data-issue-id="${update.issueId}"]`);
        if (!issueItem) {
            return;
        }

        // Update title
        const titleLink = issueItem.querySelector('.result-title');
        if (titleLink) {
            titleLink.textContent = `#${update.gitHubNumber} ${update.title}`;
        }

        // Update state badge
        const stateBadge = issueItem.querySelector('.state-badge');
        if (stateBadge) {
            stateBadge.classList.remove('state-open', 'state-closed');
            stateBadge.classList.add(update.isOpen ? 'state-open' : 'state-closed');
            stateBadge.textContent = update.isOpen ? 'Otevřený' : 'Zavřený';
        }

        // Add highlight animation
        issueItem.classList.add('issue-updated');

        // Remove highlight after animation completes
        setTimeout(function () {
            issueItem.classList.remove('issue-updated');
        }, 2000);
    }

    // Cleanup on page unload
    window.addEventListener('beforeunload', function () {
        if (connection && connection.state === signalR.HubConnectionState.Connected) {
            connection.stop();
        }
    });
})();
