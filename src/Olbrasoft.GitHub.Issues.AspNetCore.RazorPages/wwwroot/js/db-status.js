/**
 * Database status checker and import functionality
 */
(function() {
    'use strict';

    const banner = document.getElementById('dbStatusBanner');
    const statusIcon = banner?.querySelector('.db-status-icon');
    const statusMessage = banner?.querySelector('.db-status-message');
    const actionBtn = document.getElementById('dbActionBtn');
    const dismissBtn = document.getElementById('dbDismissBtn');
    const importBtn = document.getElementById('importBtn');
    const dbStats = document.getElementById('dbStats');

    // Status codes from server
    const StatusCode = {
        Healthy: 0,
        EmptyDatabase: 1,
        PendingMigrations: 2,
        NoData: 3,
        ConnectionError: 4
    };

    let currentStatus = null;

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
        } else if (status.statusCode === StatusCode.NoData) {
            dbStats.textContent = 'Žádná data';
            dbStats.style.display = 'inline';
        } else {
            dbStats.style.display = 'none';
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
                    action: importData
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

    async function importData() {
        const btn = importBtn || actionBtn;
        const originalText = btn.innerHTML;
        btn.disabled = true;
        btn.innerHTML = '<span class="import-icon spinning">&#8635;</span> Importuji...';

        // Hide action button in banner if it exists
        if (actionBtn) {
            actionBtn.style.display = 'none';
        }

        // Show progress banner
        showBanner('info', '⏳', 'Probíhá import dat z GitHubu...', null);

        try {
            const response = await fetch('/api/data/import', { method: 'POST' });
            const result = await response.json();

            if (result.success) {
                showBanner('success', '✅', result.message, null);
                setTimeout(() => {
                    checkDatabaseStatus();
                }, 2000);
            } else {
                showBanner('error', '❌', `Chyba importu: ${result.message}`, null);
            }
        } catch (error) {
            showBanner('error', '❌', `Chyba: ${error.message}`, null);
        } finally {
            btn.disabled = false;
            btn.innerHTML = originalText;
        }
    }

    // Event listeners
    dismissBtn?.addEventListener('click', hideBanner);
    importBtn?.addEventListener('click', importData);

    // Check status on page load
    document.addEventListener('DOMContentLoaded', checkDatabaseStatus);
})();
