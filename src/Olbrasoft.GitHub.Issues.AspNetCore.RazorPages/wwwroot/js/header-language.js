// Site-wide language selector for GitHub Issues Search
(function () {
    'use strict';

    const STORAGE_KEY = 'github-issues-language';
    const DEFAULT_LANGUAGE = 'cs';

    document.addEventListener('DOMContentLoaded', function () {
        initializeLanguageSelector();
    });

    function initializeLanguageSelector() {
        const selector = document.getElementById('headerLanguageSelect');
        if (!selector) return;

        // Get current language (priority: URL > localStorage > default)
        const currentLanguage = getCurrentLanguage();
        selector.value = currentLanguage;

        // Handle language change
        selector.addEventListener('change', function () {
            const newLanguage = this.value;
            setLanguage(newLanguage);
            redirectWithLanguage(newLanguage);
        });

        // Expose language for other scripts
        window.siteLanguage = currentLanguage;
    }

    function getCurrentLanguage() {
        // 1. Check URL parameter
        const urlParams = new URLSearchParams(window.location.search);
        const urlLang = urlParams.get('Lang');
        if (isValidLanguage(urlLang)) {
            // Also save to localStorage for persistence
            localStorage.setItem(STORAGE_KEY, urlLang);
            return urlLang;
        }

        // 2. Check localStorage
        const storedLang = localStorage.getItem(STORAGE_KEY);
        if (isValidLanguage(storedLang)) {
            return storedLang;
        }

        // 3. Default
        return DEFAULT_LANGUAGE;
    }

    function setLanguage(language) {
        if (isValidLanguage(language)) {
            localStorage.setItem(STORAGE_KEY, language);
            window.siteLanguage = language;
        }
    }

    function redirectWithLanguage(language) {
        const url = new URL(window.location.href);
        url.searchParams.set('Lang', language);
        window.location.href = url.toString();
    }

    function isValidLanguage(lang) {
        return lang === 'en' || lang === 'de' || lang === 'cs';
    }
})();
