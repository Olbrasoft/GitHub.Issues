/**
 * Database status checker and intelligent sync functionality
 */
(function() {
    'use strict';

    const banner = document.getElementById('dbStatusBanner');
    const statusIcon = banner?.querySelector('.db-status-icon');
    const statusMessage = banner?.querySelector('.db-status-message');
    const actionBtn = document.getElementById('dbActionBtn');
    const dismissBtn = document.getElementById('dbDismissBtn');
    const importBtn = document.getElementById('importBtn');
    const importBtnText = importBtn?.querySelector('.import-text');
    const dbStats = document.getElementById('dbStats');
    const repoSelect = document.getElementById('repoSelect');
    const fullRefreshCheckbox = document.getElementById('fullRefreshCheckbox');
    const syncStatusContainer = document.getElementById('syncStatusContainer');

    // Status codes from server
    const StatusCode = {
        Healthy: 0,
        EmptyDatabase: 1,
        PendingMigrations: 2,
        NoData: 3,
        ConnectionError: 4
    };

    let currentStatus = null;
    let repositorySyncStatus = [];

    async function checkDatabaseStatus() {
        try {
            const response = await fetch('/api/database/status');
            if (!response.ok) throw new Error('Failed to fetch status');

            const status = await response.json();
            currentStatus = status;
            updateUI(status);

            // Load repository sync status if we have data
            if (status.statusCode === StatusCode.Healthy) {
                await loadRepositorySyncStatus();
            }
        } catch (error) {
            console.error('Error checking database status:', error);
            showBanner('error', '⚠️', 'Chyba při kontrole databáze', null);
        }
    }

    async function loadRepositorySyncStatus() {
        try {
            const response = await fetch('/api/repositories/sync-status');
            if (!response.ok) throw new Error('Failed to fetch sync status');

            repositorySyncStatus = await response.json();
            populateRepoDropdown();
            updateSyncStatusDisplay();
        } catch (error) {
            console.error('Error loading repository sync status:', error);
        }
    }

    function populateRepoDropdown() {
        if (!repoSelect) return;

        // Clear existing options (except first)
        while (repoSelect.options.length > 1) {
            repoSelect.remove(1);
        }

        // Add repositories
        repositorySyncStatus.forEach(repo => {
            const option = document.createElement('option');
            option.value = repo.fullName;
            option.textContent = `${repo.fullName} (${repo.issueCount} issues)`;
            repoSelect.appendChild(option);
        });

        // Show dropdown if we have repositories
        if (repositorySyncStatus.length > 0) {
            repoSelect.closest('.sync-options')?.classList.remove('hidden');
        }
    }

    function updateSyncStatusDisplay() {
        if (!syncStatusContainer) return;

        if (repositorySyncStatus.length === 0) {
            syncStatusContainer.innerHTML = '';
            return;
        }

        const html = `
            <details class="sync-status-details">
                <summary>Stav synchronizace repozitářů</summary>
                <table class="sync-status-table">
                    <thead>
                        <tr>
                            <th>Repozitář</th>
                            <th>Issues</th>
                            <th>Poslední sync</th>
                        </tr>
                    </thead>
                    <tbody>
                        ${repositorySyncStatus.map(repo => `
                            <tr>
                                <td>${repo.fullName}</td>
                                <td>${repo.issueCount}</td>
                                <td>${formatLastSync(repo.lastSyncedAt)}</td>
                            </tr>
                        `).join('')}
                    </tbody>
                </table>
            </details>
        `;
        syncStatusContainer.innerHTML = html;
    }

    function formatLastSync(timestamp) {
        if (!timestamp) return '<span class="never-synced">nikdy</span>';

        const date = new Date(timestamp);
        const now = new Date();
        const diffMs = now - date;
        const diffMins = Math.floor(diffMs / 60000);
        const diffHours = Math.floor(diffMs / 3600000);
        const diffDays = Math.floor(diffMs / 86400000);

        if (diffMins < 1) return 'právě teď';
        if (diffMins < 60) return `před ${diffMins} min`;
        if (diffHours < 24) return `před ${diffHours} hod`;
        if (diffDays < 7) return `před ${diffDays} dny`;

        return date.toLocaleDateString('cs-CZ');
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
                    action: syncData
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
        if (importBtnText) {
            importBtnText.textContent = 'Importovat data';
        }
        // Hide sync options when importing
        document.querySelector('.sync-options')?.classList.add('hidden');
    }

    function updateButtonForSync() {
        if (importBtnText) {
            importBtnText.textContent = 'Synchronizovat';
        }
        // Show sync options when syncing
        document.querySelector('.sync-options')?.classList.remove('hidden');
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

    async function syncData() {
        const btn = importBtn || actionBtn;
        const originalHtml = btn.innerHTML;
        btn.disabled = true;
        btn.innerHTML = '<span class="import-icon spinning">&#8635;</span> Synchronizuji...';

        // Hide action button in banner if it exists
        if (actionBtn) {
            actionBtn.style.display = 'none';
        }

        // Get sync options
        const selectedRepo = repoSelect?.value || '';
        const fullRefresh = fullRefreshCheckbox?.checked || false;

        // Determine progress message
        const repoLabel = selectedRepo || 'všechny repozitáře';
        const refreshLabel = fullRefresh ? ' (plný refresh)' : '';

        // Show progress banner
        showBanner('info', '⏳', `Synchronizuji ${repoLabel}${refreshLabel}...`, null);

        try {
            const response = await fetch('/api/data/sync', {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json'
                },
                body: JSON.stringify({
                    repositoryFullName: selectedRepo || null,
                    fullRefresh: fullRefresh
                })
            });
            const result = await response.json();

            if (result.success) {
                showBanner('success', '✅', result.message, null);
                setTimeout(() => {
                    checkDatabaseStatus();
                }, 2000);
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

    // Event listeners
    dismissBtn?.addEventListener('click', hideBanner);
    importBtn?.addEventListener('click', syncData);

    // Check status on page load
    document.addEventListener('DOMContentLoaded', checkDatabaseStatus);
})();
