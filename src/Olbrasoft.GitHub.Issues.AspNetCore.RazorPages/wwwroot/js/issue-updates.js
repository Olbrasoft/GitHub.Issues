// SignalR client for real-time issue updates, progressive translations, and AI summaries
(function () {
    'use strict';

    let connection = null;
    let subscribedIssueIds = [];
    let selectedLanguage = 'cs'; // Default to Czech, will be read from page
    let fetchedBodyIds = new Set(); // Track which issue bodies have been fetched
    let bodyObserver = null; // Intersection Observer for body fetching

    // Initialize SignalR connection when DOM is ready
    document.addEventListener('DOMContentLoaded', function () {
        // Read language preference from page variable set by server
        selectedLanguage = window.selectedLanguage || 'cs';
        console.log('[issue-updates] Selected language:', selectedLanguage);

        initializeSignalR();
        initializeBodyObserver();
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

        // Handle incoming body previews (progressive loading Phase 2)
        connection.on('BodyReceived', handleBodyReceived);

        // Handle incoming AI summaries (progressive loading Phase 3)
        connection.on('SummaryReceived', handleSummaryReceived);

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
            // Trigger title translations if language is not English
            if (selectedLanguage !== 'en') {
                triggerTitleTranslations();
            } else {
                console.log('[issue-updates] Skipping title translations (English selected)');
            }
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
            body: JSON.stringify({ issueIds: subscribedIssueIds, targetLanguage: selectedLanguage })
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

        // Only apply translation if it matches the selected language
        if (data.language && data.language !== selectedLanguage) {
            console.log('[issue-updates] Ignoring translation for different language:', data.language, 'vs', selectedLanguage);
            return;
        }

        // Extract issue number from current title (format: "#123 Title text")
        const currentText = titleLink.textContent;
        const match = currentText.match(/^(#\d+)\s/);
        const issueNumberPrefix = match ? match[1] + ' ' : '';

        // Update title with translated text
        titleLink.textContent = issueNumberPrefix + data.translatedTitle;
        titleLink.classList.remove('title-translating');
        titleLink.dataset.translating = '';

        // Add highlight animation
        titleLink.classList.add('title-translated');
        setTimeout(function () {
            titleLink.classList.remove('title-translated');
        }, 2000);

        console.log('[issue-updates] Title updated for issue', data.issueId, 'language:', data.language);
    }

    function handleSummaryReceived(data) {
        console.log('[issue-updates] SummaryReceived:', data);

        // Find the ai-summary-container for this issue
        const container = document.querySelector(`.ai-summary-container[data-issue-id="${data.issueId}"]`);
        if (!container) {
            console.log('[issue-updates] AI summary container not found for issue:', data.issueId);
            return;
        }

        const bodyPreview = container.querySelector('.body-preview');
        const summaryEnDiv = container.querySelector('.ai-summary-en');
        const summaryCsDiv = container.querySelector('.ai-summary-cs');

        // Determine language from data.language field or provider
        // Backend sends language: "en", "de", "cs"
        const summaryLanguage = data.language || 'en';
        const isTargetLanguage = summaryLanguage === selectedLanguage;
        const isEnglish = summaryLanguage === 'en';

        if (isEnglish && summaryEnDiv) {
            // English summary received - show it if English is selected, or as fallback
            summaryEnDiv.textContent = data.summary;
            summaryEnDiv.style.display = selectedLanguage === 'en' ? 'block' : 'none';

            // Hide body preview when we have AI summary (only if this is the target language)
            if (selectedLanguage === 'en' && bodyPreview) {
                bodyPreview.style.display = 'none';
            }

            // Add animation only if this is the target language
            if (selectedLanguage === 'en') {
                summaryEnDiv.classList.add('summary-received');
                setTimeout(function() {
                    summaryEnDiv.classList.remove('summary-received');
                }, 2000);
            }

            console.log('[issue-updates] English summary set for issue', data.issueId);
        } else if (!isEnglish && summaryCsDiv && isTargetLanguage) {
            // Translated summary received - show if it matches selected language
            summaryCsDiv.textContent = data.summary;
            summaryCsDiv.style.display = 'block';

            // Hide English summary when translation arrives (replace, don't stack)
            if (summaryEnDiv) {
                summaryEnDiv.style.display = 'none';
            }
            if (bodyPreview) {
                bodyPreview.style.display = 'none';
            }

            // Add animation
            summaryCsDiv.classList.add('summary-received');
            setTimeout(function() {
                summaryCsDiv.classList.remove('summary-received');
            }, 2000);

            console.log('[issue-updates] Translated summary set for issue', data.issueId, 'language:', summaryLanguage);
        }
    }

    function handleBodyReceived(data) {
        console.log('[issue-updates] BodyReceived:', data);

        // Find the ai-summary-container for this issue
        const container = document.querySelector(`.ai-summary-container[data-issue-id="${data.issueId}"]`);
        if (!container) {
            console.log('[issue-updates] Container not found for issue:', data.issueId);
            return;
        }

        const bodyPreview = container.querySelector('.body-preview');
        if (!bodyPreview) {
            console.log('[issue-updates] Body preview element not found for issue:', data.issueId);
            return;
        }

        // Update body preview with received text
        bodyPreview.textContent = data.bodyPreview;
        bodyPreview.style.display = 'block';

        // Add highlight animation
        bodyPreview.classList.add('body-received');
        setTimeout(function() {
            bodyPreview.classList.remove('body-received');
        }, 2000);

        console.log('[issue-updates] Body preview updated for issue', data.issueId);
    }

    function initializeBodyObserver() {
        // Only initialize if there are issues on the page
        const issueItems = document.querySelectorAll('.result-item[data-issue-id]');
        if (issueItems.length === 0) {
            return;
        }

        console.log('[issue-updates] Initializing body observer for', issueItems.length, 'issues');

        // Create Intersection Observer to detect visible issues
        bodyObserver = new IntersectionObserver(function(entries) {
            const visibleIds = [];

            entries.forEach(function(entry) {
                if (entry.isIntersecting) {
                    const issueId = parseInt(entry.target.dataset.issueId, 10);
                    if (!isNaN(issueId) && !fetchedBodyIds.has(issueId)) {
                        visibleIds.push(issueId);
                        fetchedBodyIds.add(issueId); // Mark as fetched to avoid duplicate requests
                    }
                }
            });

            if (visibleIds.length > 0) {
                fetchBodiesForIssues(visibleIds);
            }
        }, {
            root: null, // viewport
            rootMargin: '100px', // Start fetching slightly before visible
            threshold: 0.1 // Trigger when at least 10% visible
        });

        // Observe all issue items
        issueItems.forEach(function(item) {
            bodyObserver.observe(item);
        });
    }

    function fetchBodiesForIssues(issueIds) {
        if (issueIds.length === 0) {
            return;
        }

        console.log('[issue-updates] Fetching bodies and summaries for issues:', issueIds, 'language:', selectedLanguage);

        // Fire-and-forget API call to fetch bodies and generate summaries
        fetch('/api/issues/fetch-bodies', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json'
            },
            body: JSON.stringify({ issueIds: issueIds, language: selectedLanguage })
        })
        .then(function(response) {
            if (response.ok) {
                console.log('[issue-updates] Body fetch and summarization triggered for', issueIds.length, 'issues');
            } else {
                console.error('[issue-updates] Failed to trigger body fetch:', response.status);
                // Remove from fetched set so we can retry
                issueIds.forEach(function(id) {
                    fetchedBodyIds.delete(id);
                });
            }
        })
        .catch(function(err) {
            console.error('[issue-updates] Error triggering body fetch:', err);
            // Remove from fetched set so we can retry
            issueIds.forEach(function(id) {
                fetchedBodyIds.delete(id);
            });
        });
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
