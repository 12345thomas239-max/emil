// Lille, uafhængig UI-adfærd (ingen framework) - kun til at åbne/lukke
// mobilmenuen. Rører ikke ved Blazor-render-cyklussen.
document.addEventListener('click', function (e) {
    const toggle = e.target.closest('[data-nav-toggle]');
    if (toggle) {
        const nav = document.querySelector('[data-site-nav]');
        if (nav) {
            nav.classList.toggle('open');
        }
        return;
    }

    // Luk menuen når man klikker på et link i den (mobil)
    const link = e.target.closest('[data-site-nav] a');
    if (link) {
        const nav = document.querySelector('[data-site-nav]');
        if (nav) {
            nav.classList.remove('open');
        }
    }
});


