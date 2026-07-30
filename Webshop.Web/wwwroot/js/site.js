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

document.addEventListener('DOMContentLoaded', function () {
    const hero = document.querySelector('.zhero--intro');
    if (!hero) {
        return;
    }

    const backdrop = hero.querySelector('.zhero__backdrop');

    const updateHero = () => {
        const scrollY = window.scrollY;
        const heroTop = hero.offsetTop;
        const heroHeight = hero.offsetHeight;
        const triggerEnd = heroTop + heroHeight - window.innerHeight;
        const progress = Math.min(1, Math.max(0, (scrollY - heroTop) / Math.max(1, triggerEnd - heroTop)));

        const zoom = 1.12 - (progress * 0.16);
        const blur = progress * 5;
        const opacity = 1 - (progress * 0.18);

        backdrop.style.transform = `scale(${zoom})`;
        backdrop.style.filter = `blur(${blur}px) saturate(1.08)`;
        backdrop.style.opacity = opacity.toFixed(3);
    };

    updateHero();
    window.addEventListener('scroll', updateHero, { passive: true });
    window.addEventListener('resize', updateHero);
});


