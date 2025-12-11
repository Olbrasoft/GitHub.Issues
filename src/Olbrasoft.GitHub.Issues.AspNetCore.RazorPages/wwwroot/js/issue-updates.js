// SignalR client for real-time issue updates and progressive Czech translations
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

        console.log('[issue-updates] Initializing SignalR for', issueItems.length, 'issues');

        // Create SignalR connection
        connection = new signalR.HubConnectionBuilder()
            .withUrl('/hubs/issues')
            .withAutomaticReconnect([0, 2000, 5000, 10000, 30000])
            .configureLogging(signalR.LogLevel.Information)
            .build();

        // Handle incoming issue updates
        connection.on('IssueUpdated', handleIssueUpdate);

        // Handle incoming Czech title translations
        connection.on('TitleTranslated', handleTitleTranslated);

        // Handle connection state changes
        connection.onreconnecting(function (error) {
            console.log('[issue-updates] SignalR reconnecting...', error);
        });

        connection.onreconnected(function (connectionId) {
            console.log('[issue-updates] SignalR reconnected:', connectionId);
            // Re-subscribe to issues after reconnection
            subscribeToVisibleIssues();
        });

        connection.onclose(function (error) {
            console.log('[issue-updates] SignalR connection closed:', error);
        });

        // Start connection
        startConnection();
    }

    async function startConnection() {
        try {
            await connection.start();
            console.log('[issue-updates] SignalR connected');
            await subscribeToVisibleIssues();
            // Trigger title translations after subscribing
            triggerTitleTranslations();
        } catch (err) {
            console.error('[issue-updates] SignalR connection error:', err);
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
            console.log('[issue-updates] Subscribed to', issueIds.length, 'issues:', issueIds);
        } catch (err) {
            console.error('[issue-updates] Failed to subscribe to issues:', err);
        }
    }

    function triggerTitleTranslations() {
        if (subscribedIssueIds.length === 0) {
            return;
        }

        console.log('[issue-updates] Triggering title translations for', subscribedIssueIds.length, 'issues');

        // Add "translating" indicator to all titles
        subscribedIssueIds.forEach(function (issueId) {
            const issueItem = document.querySelector(`.result-item[data-issue-id="${issueId}"]`);
            if (issueItem) {
                const titleLink = issueItem.querySelector('.result-title');
                if (titleLink && !titleLink.dataset.translating) {
                    titleLink.dataset.translating = 'true';
                    titleLink.classList.add('title-translating');
                }
            }
        });

        // Fire-and-forget API call to trigger translations
        fetch('/api/issues/translate-titles', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json'
            },
            body: JSON.stringify({ issueIds: subscribedIssueIds })
        })
        .then(response => {
            if (response.ok) {
                console.log('[issue-updates] Title translations triggered for', subscribedIssueIds.length, 'issues');
            } else {
                console.error('[issue-updates] Failed to trigger translations:', response.status);
            }
        })
        .catch(err => {
            console.error('[issue-updates] Error triggering translations:', err);
        });
    }

    function handleTitleTranslated(data) {
        console.log('[issue-updates] TitleTranslated received:', data);

        const issueItem = document.querySelector(`.result-item[data-issue-id="${data.issueId}"]`);
        if (!issueItem) {
            console.log('[issue-updates] Issue item not found for id:', data.issueId);
            return;
        }

        const titleLink = issueItem.querySelector('.result-title');
        if (!titleLink) {
            return;
        }

        // Extract issue number from current title (format: "#123 Title text")
        const currentText = titleLink.textContent;
        const match = currentText.match(/^(#\d+)\s/);
        const issueNumberPrefix = match ? match[1] + ' ' : '';

        // Update title with Czech translation
        titleLink.textContent = issueNumberPrefix + data.czechTitle;
        titleLink.classList.remove('title-translating');
        titleLink.dataset.translating = '';

        // Add highlight animation
        titleLink.classList.add('title-translated');
        setTimeout(function () {
            titleLink.classList.remove('title-translated');
        }, 2000);

        console.log('[issue-updates] Title updated for issue', data.issueId, ':', data.czechTitle);
    }

    function handleIssueUpdate(update) {
        console.log('[issue-updates] IssueUpdated received:', update);

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
