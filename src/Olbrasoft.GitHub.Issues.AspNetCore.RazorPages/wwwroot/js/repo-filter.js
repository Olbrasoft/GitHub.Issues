// Repository filter with tag-style autocomplete
(function () {
    const input = document.getElementById('repoSearchInput');
    const dropdown = document.getElementById('autocompleteDropdown');
    const tagsContainer = document.getElementById('selectedTags');
    const hiddenInput = document.getElementById('reposHidden');

    let selectedRepos = [];
    let selectedIndex = -1;
    let debounceTimer = null;

    // Initialize from server-rendered data
    function initializeFromServerData() {
        if (window.initialSelectedRepos && Array.isArray(window.initialSelectedRepos)) {
            selectedRepos = window.initialSelectedRepos.map(r => ({
                id: r.id,
                fullName: r.fullName
            }));
            renderTags();
        }
    }

    // Render selected tags
    function renderTags() {
        tagsContainer.innerHTML = selectedRepos.map(repo => `
            <span class="tag" data-id="${repo.id}">
                ${repo.fullName}
                <span class="tag-remove" data-id="${repo.id}">&times;</span>
            </span>
        `).join('');

        updateHiddenInput();
    }

    // Update hidden input with selected repo IDs
    function updateHiddenInput() {
        hiddenInput.value = selectedRepos.map(r => r.id).join(',');
        // Also update window.initialSelectedRepos so sync modal picks up current selection
        window.initialSelectedRepos = selectedRepos.map(r => ({ id: r.id, fullName: r.fullName }));
    }

    // Add a repository
    function addRepo(repo) {
        if (selectedRepos.some(r => r.id === repo.id)) return;
        selectedRepos.push(repo);
        renderTags();
        input.value = '';
        hideDropdown();
    }

    // Remove a repository
    function removeRepo(id) {
        selectedRepos = selectedRepos.filter(r => r.id !== id);
        renderTags();
    }

    // Show dropdown with results
    function showDropdown(repos) {
        if (repos.length === 0) {
            hideDropdown();
            return;
        }

        // Filter out already selected repos
        const filteredRepos = repos.filter(r => !selectedRepos.some(s => s.id === r.id));
        if (filteredRepos.length === 0) {
            hideDropdown();
            return;
        }

        dropdown.innerHTML = filteredRepos.map((repo, index) => `
            <div class="autocomplete-item${index === selectedIndex ? ' selected' : ''}"
                 data-id="${repo.id}"
                 data-fullname="${repo.fullName}">
                ${repo.fullName}
            </div>
        `).join('');

        dropdown.classList.add('show');
    }

    // Hide dropdown
    function hideDropdown() {
        dropdown.classList.remove('show');
        selectedIndex = -1;
    }

    // Search repositories
    function searchRepos(term) {
        if (!term || term.length < 1) {
            hideDropdown();
            return;
        }

        fetch(`/api/repositories/search?term=${encodeURIComponent(term)}`)
            .then(response => response.json())
            .then(repos => showDropdown(repos))
            .catch(() => hideDropdown());
    }

    // Event: Input typing with debounce
    input.addEventListener('input', function (e) {
        clearTimeout(debounceTimer);
        debounceTimer = setTimeout(() => {
            searchRepos(e.target.value.trim());
        }, 200);
    });

    // Check if dropdown is visible
    function isDropdownVisible() {
        return dropdown.classList.contains('show');
    }

    // Submit the search form
    function submitSearchForm() {
        const searchForm = document.getElementById('searchForm');
        if (searchForm) {
            searchForm.submit();
        }
    }

    // Event: Keyboard navigation
    input.addEventListener('keydown', function (e) {
        const items = dropdown.querySelectorAll('.autocomplete-item');

        if (e.key === 'ArrowDown') {
            e.preventDefault();
            selectedIndex = Math.min(selectedIndex + 1, items.length - 1);
            updateSelectedItem(items);
        } else if (e.key === 'ArrowUp') {
            e.preventDefault();
            selectedIndex = Math.max(selectedIndex - 1, 0);
            updateSelectedItem(items);
        } else if (e.key === 'Enter') {
            e.preventDefault();
            if (isDropdownVisible() && selectedIndex >= 0 && items[selectedIndex]) {
                // Dropdown is open and item is selected - add the repo
                const item = items[selectedIndex];
                addRepo({
                    id: parseInt(item.dataset.id),
                    fullName: item.dataset.fullname
                });
            } else if (!isDropdownVisible() || !e.target.value.trim()) {
                // Dropdown is closed or input is empty - submit search
                submitSearchForm();
            }
        } else if (e.key === 'Escape') {
            hideDropdown();
        } else if (e.key === 'Backspace' && !e.target.value && selectedRepos.length > 0) {
            // Remove last tag when backspace on empty input
            removeRepo(selectedRepos[selectedRepos.length - 1].id);
        }
    });

    function updateSelectedItem(items) {
        items.forEach((item, index) => {
            item.classList.toggle('selected', index === selectedIndex);
        });
    }

    // Event: Click on dropdown item
    dropdown.addEventListener('click', function (e) {
        const item = e.target.closest('.autocomplete-item');
        if (item) {
            addRepo({
                id: parseInt(item.dataset.id),
                fullName: item.dataset.fullname
            });
        }
    });

    // Event: Click on tag remove button
    tagsContainer.addEventListener('click', function (e) {
        if (e.target.classList.contains('tag-remove')) {
            removeRepo(parseInt(e.target.dataset.id));
        }
    });

    // Event: Click outside to close dropdown
    document.addEventListener('click', function (e) {
        if (!e.target.closest('.tag-input-container')) {
            hideDropdown();
        }
    });

    // Initialize
    initializeFromServerData();
})();
