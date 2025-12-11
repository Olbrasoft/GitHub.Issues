// SignalR client for progressive summary loading on issue detail page
(function () {
    'use strict';

    let connection = null;
    let issueId = null;
    let summaryLanguage = 'both'; // default: English + Czech
    let englishReceived = false;  // Track if English summary already received
    let czechReceived = false;    // Track if Czech translation already received

    // Initialize when DOM is ready
    document.addEventListener('DOMContentLoaded', function () {
        initializeDetailUpdates();
    });

    function initializeDetailUpdates() {
        console.log('[detail-updates] Initializing...');
        const container = document.getElementById('ai-summary-container');
        if (!container) {
            console.log('[detail-updates] No ai-summary-container found');
            return;
        }

        issueId = parseInt(container.dataset.issueId, 10);
        const summaryPending = container.dataset.summaryPending === 'true';
        summaryLanguage = container.dataset.summaryLanguage || 'both';

        console.log('[detail-updates] Issue ID:', issueId, 'Summary pending:', summaryPending, 'Language:', summaryLanguage);

        if (!issueId || isNaN(issueId)) {
            console.log('[detail-updates] Invalid issue ID');
            return;
        }

        // Only connect to SignalR if summary is pending
        if (summaryPending) {
            console.log('[detail-updates] Summary is pending, initializing SignalR...');
            initializeSignalR();
        } else {
            console.log('[detail-updates] Summary not pending, skipping SignalR');
        }
    }

    function initializeSignalR() {
        console.log('[detail-updates] Creating SignalR connection to /hubs/issues...');
        // Create SignalR connection
        connection = new signalR.HubConnectionBuilder()
            .withUrl('/hubs/issues')
            .withAutomaticReconnect([0, 2000, 5000, 10000, 30000])
            .configureLogging(signalR.LogLevel.Information)
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
        // Pass language preference: "en" (English only) or "both" (English + Czech)
        fetch(`/api/issues/${issueId}/generate-summary?language=${summaryLanguage}`, {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json'
            }
        })
        .then(response => {
            if (response.ok) {
                console.log('Summary generation triggered for issue', issueId, 'language:', summaryLanguage);
            } else {
                console.error('Failed to trigger summary generation:', response.status);
            }
        })
        .catch(err => {
            console.error('Error triggering summary generation:', err);
        });
    }

    function handleSummaryReceived(data) {
        console.log('[detail-updates] SummaryReceived event received!');
        console.log('[detail-updates] Data:', JSON.stringify(data));
        console.log('[detail-updates] Expected issueId:', issueId, 'Received:', data.issueId);

        if (data.issueId !== issueId) {
            console.log('[detail-updates] Issue ID mismatch, ignoring');
            return;
        }

        const container = document.getElementById('ai-summary-container');
        if (!container) {
            return;
        }

        // Determine if this is English or Czech summary
        const isEnglish = data.language === 'en' || (!data.language);
        const isCzech = data.language === 'cs';

        console.log('[detail-updates] Language:', data.language, 'isEnglish:', isEnglish, 'isCzech:', isCzech);

        if (isEnglish) {
            englishReceived = true;
            console.log('[detail-updates] English summary received');
        }
        if (isCzech) {
            czechReceived = true;
            console.log('[detail-updates] Czech summary received');
        }

        // Replace loading indicator with actual summary
        const langLabel = isCzech ? 'AI Shrnutí (česky)' : 'AI Summary';
        container.innerHTML = `
            <div class="ai-summary">
                <div class="ai-summary-header">
                    <span class="ai-icon">🤖</span>
                    <span class="ai-label">${langLabel}</span>
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

        // Only disconnect when we've received all expected summaries
        const shouldDisconnect = shouldDisconnectSignalR();
        console.log('[detail-updates] Should disconnect:', shouldDisconnect,
            'summaryLanguage:', summaryLanguage,
            'englishReceived:', englishReceived,
            'czechReceived:', czechReceived);

        if (shouldDisconnect && connection) {
            console.log('[detail-updates] All expected summaries received, disconnecting SignalR');
            connection.stop();
        }
    }

    function shouldDisconnectSignalR() {
        // If language is 'en' only, disconnect after English
        if (summaryLanguage === 'en') {
            return englishReceived;
        }
        // If language is 'both', wait for both English and Czech
        if (summaryLanguage === 'both') {
            return englishReceived && czechReceived;
        }
        // If language is 'cs' only (unlikely but possible), disconnect after Czech
        if (summaryLanguage === 'cs') {
            return czechReceived;
        }
        // Default: disconnect after any summary
        return true;
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
