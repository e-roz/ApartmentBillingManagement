// File: wwwroot/js/theme-switcher.js

(function () {
    'use strict';

    const themeToggle = document.getElementById('theme-toggle');
    const body = document.body;
    const themeIcon = themeToggle.querySelector('i');
    const themeLabel = themeToggle.querySelector('.nav-label');

    const lightIcon = 'bi-sun-fill';
    const darkIcon = 'bi-moon-stars-fill';
    const lightLabel = 'Light Mode';
    const darkLabel = 'Dark Mode';

    // Function to apply the theme
    function applyTheme(theme) {
        if (theme === 'dark') {
            body.classList.add('dark-mode');
            themeIcon.classList.remove(lightIcon);
            themeIcon.classList.add(darkIcon);
            themeLabel.textContent = darkLabel;
        } else {
            body.classList.remove('dark-mode');
            themeIcon.classList.remove(darkIcon);
            themeIcon.classList.add(lightIcon);
            themeLabel.textContent = lightLabel;
        }
    }

    // Function to handle the theme toggle click
    function toggleTheme() {
        const currentTheme = body.classList.contains('dark-mode') ? 'dark' : 'light';
        const newTheme = currentTheme === 'dark' ? 'light' : 'dark';
        
        localStorage.setItem('theme', newTheme);
        applyTheme(newTheme);
    }

    // Event listener for the theme toggle button
    if (themeToggle) {
        themeToggle.addEventListener('click', toggleTheme);
    }

    // On page load, apply the saved theme or the default (light)
    document.addEventListener('DOMContentLoaded', function () {
        const savedTheme = localStorage.getItem('theme') || 'light';
        applyTheme(savedTheme);
    });

})();
