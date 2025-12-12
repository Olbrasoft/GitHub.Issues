/**
 * Database status checker and intelligent sync functionality with modal dialog
 * Includes authentication status management
 */
(function() {
    'use strict';

    // DOM Elements - Auth
    const authControls = document.getElementById('authControls');
    const authUser = document.getElementById('authUser');
    const loginLink = document.getElementById('loginLink');
    const logoutLink = document.getElementById('logoutLink');

    // DOM Elements - Status banner
    const banner = document.getElementById('dbStatusBanner');
    const statusIcon = banner?.querySelector('.db-status-icon');
    const statusMessage = banner?.querySelector('.db-status-message');
    const actionBtn = document.getElementById('dbActionBtn');
    const dismissBtn = document.getElementById('dbDismissBtn');
    const dbStats = document.getElementById('dbStats');

    // DOM Elements - Sync button
    const syncBtn = document.getElementById('syncBtn');
    const syncBtnText = syncBtn?.querySelector('.sync-text');

    // Auth state
    let isOwner = false;

    // DOM Elements - Sync Modal
    const syncModal = document.getElementById('syncModal');
    const modalCloseBtn = document.getElementById('modalCloseBtn');
    const modalCancelBtn = document.getElementById('modalCancelBtn');
    const modalSyncBtn = document.getElementById('modalSyncBtn');
    const modalFullRefreshCheckbox = document.getElementById('modalFullRefreshCheckbox');

    // DOM Elements - Sync Modal Tag Input
    const syncTagInputContainer = document.getElementById('syncTagInputContainer');
    const syncSelectedTags = document.getElementById('syncSelectedTags');
    const syncRepoSearchInput = document.getElementById('syncRepoSearchInput');
    const syncAutocompleteDropdown = document.getElementById('syncAutocompleteDropdown');

    // DOM Elements - Sync Result Modal
    const syncResultModal = document.getElementById('syncResultModal');
    const syncResultTitle = document.getElementById('syncResultTitle');
    const syncResultContent = document.getElementById('syncResultContent');
    const resultCloseBtn = document.getElementById('resultCloseBtn');
    const resultOkBtn = document.getElementById('resultOkBtn');

    // Status codes from server
    const StatusCode = {
        Healthy: 0,
        EmptyDatabase: 1,
        PendingMigrations: 2,
        NoData: 3,
        ConnectionError: 4
    };

    let currentStatus = null;
    let syncSelectedRepos = []; // Selected repos in sync modal
    let syncSelectedIndex = -1;
    let syncDebounceTimer = null;

    // ===== Authentication Functions =====

    async function checkAuthStatus() {
        try {
            const response = await fetch('/api/auth/status');
            if (!response.ok) throw new Error('Failed to fetch auth status');

            const auth = await response.json();
            isOwner = auth.isOwner;

            // Update UI based on auth status
            if (auth.isAuthenticated) {
                authControls.style.display = 'flex';
                authUser.textContent = auth.username;
                loginLink.style.display = 'none';

                // Show sync button only for owner
                if (auth.isOwner && syncBtn) {
                    syncBtn.style.display = 'inline-flex';
                }
            } else {
                authControls.style.display = 'none';
                loginLink.style.display = 'inline-flex';
                if (syncBtn) {
                    syncBtn.style.display = 'none';
                }
            }
        } catch (error) {
            console.error('Error checking auth status:', error);
            // Show login link on error
            loginLink.style.display = 'inline-flex';
        }
    }

    // ===== Database Status Functions =====

    async function checkDatabaseStatus() {
        try {
            const response = await fetch('/api/database/status');
            if (!response.ok) throw new Error('Failed to fetch status');

            const status = await response.json();
            currentStatus = status;
            updateUI(status);
        } catch (error) {
            console.error('Error checking database status:', error);
            showBanner('error', '⚠️', 'Chyba při kontrole databáze', null);
        }
    }

    function updateUI(status) {
        // Update stats display
        if (status.statusCode === StatusCode.Healthy) {
            dbStats.textContent = `${status.issueCount} issues | ${status.repositoryCount} repozitářů`;
            dbStats.style.display = 'inline';
            updateButtonForSync();
        } else if (status.statusCode === StatusCode.NoData) {
            dbStats.textContent = 'Žádná data';
            dbStats.style.display = 'inline';
            updateButtonForImport();
        } else {
            dbStats.style.display = 'none';
            updateButtonForImport();
        }

        // Show appropriate banner based on status
        switch (status.statusCode) {
            case StatusCode.EmptyDatabase:
                showBanner('warning', '⚠️', status.statusMessage, {
                    text: 'Vytvořit tabulky',
                    action: applyMigrations
                });
                break;

            case StatusCode.PendingMigrations:
                showBanner('warning', '⚠️', status.statusMessage, {
                    text: 'Provést migraci',
                    action: applyMigrations
                });
                break;

            case StatusCode.NoData:
                showBanner('info', 'ℹ️', status.statusMessage, {
                    text: 'Importovat data',
                    action: () => syncDataDirect() // Direct sync when no data
                });
                break;

            case StatusCode.ConnectionError:
                showBanner('error', '❌', status.statusMessage, null);
                break;

            case StatusCode.Healthy:
            default:
                hideBanner();
                break;
        }
    }

    function updateButtonForImport() {
        if (syncBtnText) {
            syncBtnText.textContent = 'Importovat data';
        }
    }

    function updateButtonForSync() {
        if (syncBtnText) {
            syncBtnText.textContent = 'Synchronizovat';
        }
    }

    function showBanner(type, icon, message, action) {
        banner.className = `db-status-banner db-status-${type}`;
        banner.style.display = 'block';
        statusIcon.textContent = icon;
        statusMessage.textContent = message;

        if (action) {
            actionBtn.textContent = action.text;
            actionBtn.onclick = action.action;
            actionBtn.style.display = 'inline-block';
        } else {
            actionBtn.style.display = 'none';
        }
    }

    function hideBanner() {
        banner.style.display = 'none';
    }

    async function applyMigrations() {
        actionBtn.disabled = true;
        actionBtn.textContent = 'Probíhá...';

        try {
            const response = await fetch('/api/database/migrate', { method: 'POST' });
            const result = await response.json();

            if (result.success) {
                showBanner('success', '✅', `Migrace dokončena. Aplikováno: ${result.migrationsApplied}`, null);
                setTimeout(() => {
                    checkDatabaseStatus();
                }, 2000);
            } else {
                showBanner('error', '❌', `Chyba migrace: ${result.errorMessage}`, null);
            }
        } catch (error) {
            showBanner('error', '❌', `Chyba: ${error.message}`, null);
        } finally {
            actionBtn.disabled = false;
        }
    }

    // ===== Sync Modal Functions =====

    function openSyncModal() {
        // If no data, do direct sync without modal
        if (currentStatus?.statusCode === StatusCode.NoData) {
            syncDataDirect();
            return;
        }

        // Pre-fill from search filter if available
        if (window.initialSelectedRepos && Array.isArray(window.initialSelectedRepos)) {
            syncSelectedRepos = window.initialSelectedRepos.map(r => ({
                id: r.id,
                fullName: r.fullName
            }));
        } else {
            syncSelectedRepos = [];
        }

        renderSyncTags();
        modalFullRefreshCheckbox.checked = false;
        syncModal.style.display = 'flex';
        syncRepoSearchInput.focus();
    }

    function closeSyncModal() {
        syncModal.style.display = 'none';
        hideSyncDropdown();
    }

    function renderSyncTags() {
        syncSelectedTags.innerHTML = syncSelectedRepos.map(repo => `
            <span class="tag" data-id="${repo.id}">
                ${repo.fullName}
                <span class="tag-remove" data-id="${repo.id}">&times;</span>
            </span>
        `).join('');
    }

    function addSyncRepo(repo) {
        if (syncSelectedRepos.some(r => r.id === repo.id)) return;
        syncSelectedRepos.push(repo);
        renderSyncTags();
        syncRepoSearchInput.value = '';
        hideSyncDropdown();
    }

    function removeSyncRepo(id) {
        syncSelectedRepos = syncSelectedRepos.filter(r => r.id !== id);
        renderSyncTags();
    }

    function showSyncDropdown(repos) {
        if (repos.length === 0) {
            hideSyncDropdown();
            return;
        }

        const filteredRepos = repos.filter(r => !syncSelectedRepos.some(s => s.id === r.id));
        if (filteredRepos.length === 0) {
            hideSyncDropdown();
            return;
        }

        syncAutocompleteDropdown.innerHTML = filteredRepos.map((repo, index) => `
            <div class="autocomplete-item${index === syncSelectedIndex ? ' selected' : ''}"
                 data-id="${repo.id}"
                 data-fullname="${repo.fullName}">
                ${repo.fullName}
            </div>
        `).join('');

        syncAutocompleteDropdown.classList.add('show');
    }

    function hideSyncDropdown() {
        syncAutocompleteDropdown.classList.remove('show');
        syncSelectedIndex = -1;
    }

    function searchSyncRepos(term) {
        if (!term || term.length < 1) {
            hideSyncDropdown();
            return;
        }

        fetch(`/api/repositories/search?term=${encodeURIComponent(term)}`)
            .then(response => response.json())
            .then(repos => showSyncDropdown(repos))
            .catch(() => hideSyncDropdown());
    }

    // ===== Sync Execution =====

    // Direct sync (no modal) - used when database is empty
    async function syncDataDirect() {
        const btn = syncBtn || actionBtn;
        const originalHtml = btn.innerHTML;
        btn.disabled = true;
        btn.innerHTML = '<span class="sync-icon spinning">&#8635;</span> Synchronizuji...';

        if (actionBtn) {
            actionBtn.style.display = 'none';
        }

        showBanner('info', '⏳', 'Synchronizuji všechny repozitáře...', null);

        try {
            const response = await fetch('/api/data/sync', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({
                    repositoryFullNames: null,
                    fullRefresh: false
                })
            });
            const result = await response.json();

            if (result.success) {
                showSyncResult(result);
            } else {
                showBanner('error', '❌', `Chyba synchronizace: ${result.message}`, null);
            }
        } catch (error) {
            showBanner('error', '❌', `Chyba: ${error.message}`, null);
        } finally {
            btn.disabled = false;
            btn.innerHTML = originalHtml;
        }
    }

    // Sync from modal
    async function syncDataFromModal() {
        closeSyncModal();

        const btn = syncBtn;
        const originalHtml = btn.innerHTML;
        btn.disabled = true;
        btn.innerHTML = '<span class="sync-icon spinning">&#8635;</span> Synchronizuji...';

        const fullRefresh = modalFullRefreshCheckbox.checked;
        const repoNames = syncSelectedRepos.map(r => r.fullName);
        const repoLabel = repoNames.length > 0 ? repoNames.join(', ') : 'všechny repozitáře';
        const refreshLabel = fullRefresh ? ' (plný refresh)' : '';

        showBanner('info', '⏳', `Synchronizuji ${repoLabel}${refreshLabel}...`, null);

        try {
            const response = await fetch('/api/data/sync', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({
                    repositoryFullNames: repoNames.length > 0 ? repoNames : null,
                    fullRefresh: fullRefresh
                })
            });
            const result = await response.json();

            if (result.success) {
                showSyncResult(result);
            } else {
                showBanner('error', '❌', `Chyba synchronizace: ${result.message}`, null);
            }
        } catch (error) {
            showBanner('error', '❌', `Chyba: ${error.message}`, null);
        } finally {
            btn.disabled = false;
            btn.innerHTML = originalHtml;
        }
    }

    // ===== Sync Result Modal =====

    function showSyncResult(result) {
        hideBanner();

        syncResultTitle.textContent = result.success ? 'Synchronizace dokončena' : 'Synchronizace selhala';

        let html = '';

        if (result.statistics) {
            const stats = result.statistics;
            html += `
                <div class="sync-stats">
                    <div class="sync-stat-row">
                        <span class="sync-stat-label">API dotazů:</span>
                        <span class="sync-stat-value">${stats.apiCalls || 0}</span>
                    </div>
                    <div class="sync-stat-row">
                        <span class="sync-stat-label">Nalezeno issues:</span>
                        <span class="sync-stat-value">${stats.totalFound || 0}</span>
                    </div>
                    <div class="sync-stat-row">
                        <span class="sync-stat-label">Nově přidáno:</span>
                        <span class="sync-stat-value">${stats.created || 0}</span>
                    </div>
                    <div class="sync-stat-row">
                        <span class="sync-stat-label">Aktualizováno:</span>
                        <span class="sync-stat-value">${stats.updated || 0}</span>
                    </div>
                    <div class="sync-stat-row">
                        <span class="sync-stat-label">Beze změny:</span>
                        <span class="sync-stat-value">${stats.unchanged || 0}</span>
                    </div>
                    ${stats.embeddingsFailed > 0 ? `
                    <div class="sync-stat-row sync-stat-warning">
                        <span class="sync-stat-label">Selhalo embeddingů:</span>
                        <span class="sync-stat-value">${stats.embeddingsFailed}</span>
                    </div>
                    ` : ''}
                </div>
            `;

            if (stats.sinceTimestamp) {
                html += `
                    <div class="sync-timestamp">
                        <small>Inkrementální sync od: ${new Date(stats.sinceTimestamp).toLocaleString('cs-CZ')}</small>
                    </div>
                `;
            }
        } else {
            html = `<p>${result.message}</p>`;
        }

        syncResultContent.innerHTML = html;
        syncResultModal.style.display = 'flex';

        // Refresh database status after sync
        setTimeout(() => {
            checkDatabaseStatus();
        }, 500);
    }

    function closeSyncResultModal() {
        syncResultModal.style.display = 'none';
    }

    // ===== Event Listeners =====

    // Sync button click
    syncBtn?.addEventListener('click', openSyncModal);

    // Dismiss banner
    dismissBtn?.addEventListener('click', hideBanner);

    // Modal close buttons
    modalCloseBtn?.addEventListener('click', closeSyncModal);
    modalCancelBtn?.addEventListener('click', closeSyncModal);
    modalSyncBtn?.addEventListener('click', syncDataFromModal);

    // Result modal close buttons
    resultCloseBtn?.addEventListener('click', closeSyncResultModal);
    resultOkBtn?.addEventListener('click', closeSyncResultModal);

    // Click outside modal to close
    syncModal?.addEventListener('click', function(e) {
        if (e.target === syncModal) {
            closeSyncModal();
        }
    });

    syncResultModal?.addEventListener('click', function(e) {
        if (e.target === syncResultModal) {
            closeSyncResultModal();
        }
    });

    // Sync modal tag input - typing
    syncRepoSearchInput?.addEventListener('input', function(e) {
        clearTimeout(syncDebounceTimer);
        syncDebounceTimer = setTimeout(() => {
            searchSyncRepos(e.target.value.trim());
        }, 200);
    });

    // Sync modal tag input - keyboard navigation
    syncRepoSearchInput?.addEventListener('keydown', function(e) {
        const items = syncAutocompleteDropdown.querySelectorAll('.autocomplete-item');

        if (e.key === 'ArrowDown') {
            e.preventDefault();
            syncSelectedIndex = Math.min(syncSelectedIndex + 1, items.length - 1);
            updateSyncSelectedItem(items);
        } else if (e.key === 'ArrowUp') {
            e.preventDefault();
            syncSelectedIndex = Math.max(syncSelectedIndex - 1, 0);
            updateSyncSelectedItem(items);
        } else if (e.key === 'Enter') {
            e.preventDefault();
            if (syncSelectedIndex >= 0 && items[syncSelectedIndex]) {
                const item = items[syncSelectedIndex];
                addSyncRepo({
                    id: parseInt(item.dataset.id),
                    fullName: item.dataset.fullname
                });
            }
        } else if (e.key === 'Escape') {
            hideSyncDropdown();
        } else if (e.key === 'Backspace' && !e.target.value && syncSelectedRepos.length > 0) {
            removeSyncRepo(syncSelectedRepos[syncSelectedRepos.length - 1].id);
        }
    });

    function updateSyncSelectedItem(items) {
        items.forEach((item, index) => {
            item.classList.toggle('selected', index === syncSelectedIndex);
        });
    }

    // Sync modal dropdown - click
    syncAutocompleteDropdown?.addEventListener('click', function(e) {
        const item = e.target.closest('.autocomplete-item');
        if (item) {
            addSyncRepo({
                id: parseInt(item.dataset.id),
                fullName: item.dataset.fullname
            });
        }
    });

    // Sync modal tags - remove click
    syncSelectedTags?.addEventListener('click', function(e) {
        if (e.target.classList.contains('tag-remove')) {
            removeSyncRepo(parseInt(e.target.dataset.id));
        }
    });

    // Click outside dropdown to close
    syncTagInputContainer?.addEventListener('click', function(e) {
        if (e.target === syncTagInputContainer || e.target === syncRepoSearchInput) {
            // Keep dropdown
        }
    });

    document.addEventListener('click', function(e) {
        if (!e.target.closest('#syncTagInputContainer')) {
            hideSyncDropdown();
        }
    });

    // Check status on page load
    document.addEventListener('DOMContentLoaded', async () => {
        await checkAuthStatus();
        await checkDatabaseStatus();
    });
})();
