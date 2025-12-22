// SignalR client for real-time issue updates, progressive translations, and AI summaries
(function () {
    'use strict';

    let connection = null;
    let subscribedIssueIds = [];
    let selectedLanguage = 'cs'; // Default to Czech, will be read from page
    let fetchedBodyIds = new Set(); // Track which issue bodies have been fetched
    let bodyObserver = null; // Intersection Observer for body fetching
    let isSignalRSubscribed = false; // Track if SignalR subscription is complete
    let pendingBodyFetches = []; // Queue for body fetches pending subscription
    let keepAliveInterval = null; // Client-side keep-alive interval
    let currentRepoFilter = ''; // Current repository filter from search form

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

        // Handle new issues added via webhook (auto-refresh search results)
        connection.on('NewIssueAdded', handleNewIssueAdded);

        // Handle connection state changes
        connection.onreconnecting(function (error) {
            console.log('[issue-updates] SignalR reconnecting...', error);
            isSignalRSubscribed = false; // Reset subscription flag during reconnection
            updateConnectionStatus(false);
            stopKeepAlive();
        });

        connection.onreconnected(function (connectionId) {
            console.log('[issue-updates] SignalR reconnected:', connectionId);
            updateConnectionStatus(true);
            startKeepAlive();
            // Re-subscribe to issues after reconnection
            subscribeToVisibleIssues();
        });

        connection.onclose(function (error) {
            console.log('[issue-updates] SignalR connection closed:', error);
            updateConnectionStatus(false);
            stopKeepAlive();
        });

        // Start connection
        startConnection();
    }

    async function startConnection() {
        try {
            await connection.start();
            console.log('[issue-updates] SignalR connected');
            updateConnectionStatus(true);
            startKeepAlive();

            // Get current repository filter from the page (if any)
            currentRepoFilter = getRepoFilterFromPage();

            // Subscribe to search results for auto-refresh when new issues are added
            await subscribeToSearchResults(currentRepoFilter);

            await subscribeToVisibleIssues();
            // Trigger title translations if language is not English
            if (selectedLanguage !== 'en') {
                triggerTitleTranslations();
            } else {
                console.log('[issue-updates] Skipping title translations (English selected)');
            }
        } catch (err) {
            console.error('[issue-updates] SignalR connection error:', err);
            updateConnectionStatus(false);
            // Retry connection after 5 seconds
            setTimeout(startConnection, 5000);
        }
    }

    function getRepoFilterFromPage() {
        // Get the repository full name from the displayed tag (not the hidden ID input)
        // The tag structure is: <div class="tag"><text>owner/repo</text><span class="tag-remove">×</span></div>
        var repoTag = document.querySelector('.tags .tag');
        if (repoTag) {
            // Get the first text node that contains the repo name (before the × button)
            var repoName = repoTag.childNodes[0];
            if (repoName && repoName.textContent) {
                var name = repoName.textContent.trim();
                if (name && name !== '×') {
                    console.log('[issue-updates] Detected repo filter from tag:', name);
                    return name;
                }
            }
        }
        return '';
    }

    async function subscribeToSearchResults(repoFilter) {
        try {
            await connection.invoke('SubscribeToSearchResults', repoFilter || '');
            console.log('[issue-updates] Subscribed to search results', repoFilter ? 'for repo: ' + repoFilter : '(all repos)');
        } catch (err) {
            console.error('[issue-updates] Failed to subscribe to search results:', err);
        }
    }

    function startKeepAlive() {
        // Clear any existing interval
        if (keepAliveInterval) {
            clearInterval(keepAliveInterval);
        }

        // Send ping every 30 seconds to keep connection alive through proxies
        keepAliveInterval = setInterval(function() {
            if (connection && connection.state === signalR.HubConnectionState.Connected) {
                // Use invoke with a no-op to keep the connection active
                // This helps bypass Azure/proxy idle timeouts
                connection.invoke('SubscribeToIssues', [])
                    .catch(function(err) {
                        console.log('[issue-updates] Keep-alive ping failed:', err);
                    });
            }
        }, 30000);
    }

    function stopKeepAlive() {
        if (keepAliveInterval) {
            clearInterval(keepAliveInterval);
            keepAliveInterval = null;
        }
    }

    function updateConnectionStatus(connected) {
        const statusIndicator = document.querySelector('.signalr-status');
        if (statusIndicator) {
            statusIndicator.classList.toggle('connected', connected);
            statusIndicator.classList.toggle('disconnected', !connected);
            statusIndicator.title = connected ? 'Připojeno (real-time updates)' : 'Odpojeno (reconnecting...)';
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
            isSignalRSubscribed = true;
            console.log('[issue-updates] Subscribed to', issueIds.length, 'issues:', issueIds);

            // Process any pending body fetches that were queued before subscription
            if (pendingBodyFetches.length > 0) {
                console.log('[issue-updates] Processing', pendingBodyFetches.length, 'pending body fetches');
                const pending = pendingBodyFetches;
                pendingBodyFetches = [];
                fetchBodiesForIssues(pending);
            }
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

        // Validate data structure
        if (!data || typeof data.issueId === 'undefined') {
            console.error('[issue-updates] Invalid SummaryReceived data - missing issueId:', data);
            return;
        }

        // Find the ai-summary-container for this issue
        const container = document.querySelector(`.ai-summary-container[data-issue-id="${data.issueId}"]`);
        if (!container) {
            console.log('[issue-updates] AI summary container not found for issue:', data.issueId);
            // Log all containers to help debug
            const allContainers = document.querySelectorAll('.ai-summary-container');
            console.log('[issue-updates] Available containers:', Array.from(allContainers).map(c => c.dataset.issueId));
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

        // Determine cache indicator
        const isFromCache = data.provider === 'cache';
        const cacheIndicator = isFromCache ? '⚡ ' : '✨ ';
        const cacheTitle = isFromCache ? 'Načteno z cache' : 'Nově vygenerováno';

        if (isEnglish && summaryEnDiv) {
            // English summary received - show it if English is selected, or as fallback
            summaryEnDiv.innerHTML = '<span class="' + (isFromCache ? 'cache-indicator' : 'fresh-indicator') + '" title="' + cacheTitle + '">' + cacheIndicator + '</span>' + escapeHtml(data.summary);
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
            summaryCsDiv.innerHTML = '<span class="' + (isFromCache ? 'cache-indicator' : 'fresh-indicator') + '" title="' + cacheTitle + '">' + cacheIndicator + '</span>' + escapeHtml(data.summary);
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
                if (isSignalRSubscribed) {
                    fetchBodiesForIssues(visibleIds);
                } else {
                    // Queue body fetches until SignalR is subscribed
                    console.log('[issue-updates] Queuing', visibleIds.length, 'body fetches (waiting for SignalR)');
                    pendingBodyFetches = pendingBodyFetches.concat(visibleIds);
                }
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

    function handleNewIssueAdded(data) {
        console.log('[issue-updates] NewIssueAdded received:', data);

        // Check if the new issue matches our current repository filter
        if (currentRepoFilter && data.repositoryFullName !== currentRepoFilter) {
            console.log('[issue-updates] Ignoring new issue - different repo:', data.repositoryFullName, 'vs filter:', currentRepoFilter);
            return;
        }

        // Show "new issues available" banner instead of auto-refreshing
        showNewIssueBanner(data);
    }

    function showNewIssueBanner(issueData) {
        // Check if banner already exists
        var banner = document.getElementById('newIssueBanner');
        if (!banner) {
            // Create the banner
            banner = document.createElement('div');
            banner.id = 'newIssueBanner';
            banner.className = 'new-issue-banner';
            banner.innerHTML = `
                <div class="new-issue-content">
                    <span class="new-issue-icon">🆕</span>
                    <span class="new-issue-message">Nový issue: <strong>#${issueData.gitHubNumber}</strong> ${issueData.title}</span>
                    <button class="new-issue-refresh-btn" onclick="location.reload()">Obnovit</button>
                    <button class="new-issue-dismiss-btn" onclick="dismissNewIssueBanner()">&times;</button>
                </div>
            `;

            // Insert at the top of results container
            var resultsContainer = document.querySelector('.results-container');
            if (resultsContainer) {
                resultsContainer.insertBefore(banner, resultsContainer.firstChild);
            } else {
                // Fallback: insert after search container
                var searchContainer = document.querySelector('.search-container');
                if (searchContainer && searchContainer.nextSibling) {
                    searchContainer.parentNode.insertBefore(banner, searchContainer.nextSibling);
                }
            }

            // Add animation
            setTimeout(function() {
                banner.classList.add('show');
            }, 10);
        } else {
            // Update existing banner with new count
            var message = banner.querySelector('.new-issue-message');
            if (message) {
                var currentText = message.textContent;
                if (currentText.includes('nových issues')) {
                    // Increment count
                    var match = currentText.match(/(\d+) nových issues/);
                    var count = match ? parseInt(match[1]) + 1 : 2;
                    message.innerHTML = `<strong>${count} nových issues</strong> k dispozici`;
                } else {
                    // Change from single to multiple
                    message.innerHTML = '<strong>2 nových issues</strong> k dispozici';
                }
            }
        }
    }

    // Global function for dismissing banner
    window.dismissNewIssueBanner = function() {
        var banner = document.getElementById('newIssueBanner');
        if (banner) {
            banner.classList.remove('show');
            setTimeout(function() {
                banner.remove();
            }, 300);
        }
    };

    // Escape HTML to prevent XSS
    function escapeHtml(text) {
        var div = document.createElement('div');
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
