// IMSB site behaviour (Bootstrap 5.3, no jQuery dependency)
(function () {
    'use strict';

    const root = document.documentElement;
    root.classList.add('js');

    // announcements in the page's language (window.IMSB_I18N, from the layout; the English text is the key)
    const I18N = window.IMSB_I18N || {};
    const T = (s) => I18N[s] || s;

    const store = {
        get(k) { try { return localStorage.getItem(k); } catch { return null; } },
        set(k, v) { try { localStorage.setItem(k, v); } catch { /* ignore */ } }
    };
    const announce = (msg) => {
        const el = document.getElementById('a11y-status');
        if (el) el.textContent = msg;
    };

    // ---------- Text size ----------
    const sizes = ['text-sm', '', 'text-lg', 'text-xl'];
    let sizeIdx = Math.max(0, sizes.indexOf(store.get('imsb-text-size') || ''));
    const applySize = () => {
        sizes.forEach(c => c && root.classList.remove(c));
        if (sizes[sizeIdx]) root.classList.add(sizes[sizeIdx]);
        store.set('imsb-text-size', sizes[sizeIdx]);
    };
    applySize();
    document.querySelectorAll('[data-text-size]').forEach(btn => {
        btn.addEventListener('click', () => {
            const dir = btn.dataset.textSize;
            if (dir === 'up') sizeIdx = Math.min(sizes.length - 1, sizeIdx + 1);
            else if (dir === 'down') sizeIdx = Math.max(0, sizeIdx - 1);
            else sizeIdx = 1;
            applySize();
            announce(T('Text size changed'));
        });
    });

    // ---------- High contrast ----------
    const contrastBtn = document.querySelector('[data-contrast]');
    const setContrast = (on) => {
        root.classList.toggle('high-contrast', on);
        if (contrastBtn) {
            contrastBtn.setAttribute('aria-pressed', String(on));
            contrastBtn.classList.toggle('active', on);
        }
        store.set('imsb-contrast', on ? '1' : '0');
    };
    setContrast(store.get('imsb-contrast') === '1');
    contrastBtn?.addEventListener('click', () => {
        const on = !root.classList.contains('high-contrast');
        setContrast(on);
        announce(T(on ? 'High contrast on' : 'High contrast off'));
    });

    // ---------- Language ----------
    // The English / తెలుగు buttons are links to Home/SetLanguage: the server saves the choice and shows the page in it.

    // ---------- Hero carousel pause / play ----------
    const heroEl = document.getElementById('heroCarousel');
    const pauseBtn = document.querySelector('[data-carousel-pause]');
    if (heroEl && pauseBtn && window.bootstrap) {
        const carousel = bootstrap.Carousel.getOrCreateInstance(heroEl);
        let paused = window.matchMedia('(prefers-reduced-motion: reduce)').matches;
        const render = () => {
            pauseBtn.innerHTML = paused ? '<i class="bi bi-play-fill" aria-hidden="true"></i>' : '<i class="bi bi-pause-fill" aria-hidden="true"></i>';
            pauseBtn.setAttribute('aria-label', T(paused ? 'Play slide rotation' : 'Pause slide rotation'));
        };
        if (paused) carousel.pause();
        render();
        pauseBtn.addEventListener('click', () => {
            paused = !paused;
            paused ? carousel.pause() : carousel.cycle();
            render();
        });
    }

    // ---------- Gallery lightbox ----------
    const lightbox = document.getElementById('lightbox');
    if (lightbox) {
        const items = Array.from(document.querySelectorAll('.gallery-item'));
        const img = lightbox.querySelector('.lightbox-img');
        const caption = lightbox.querySelector('#lightbox-caption');
        let current = 0;
        const show = (i) => {
            current = (i + items.length) % items.length;
            const a = items[current];
            img.src = a.getAttribute('href');
            img.alt = a.dataset.caption || '';
            caption.textContent = a.dataset.caption || '';
        };
        items.forEach(a => a.addEventListener('click', e => e.preventDefault()));
        lightbox.addEventListener('show.bs.modal', e => {
            const trigger = e.relatedTarget;
            show(trigger ? parseInt(trigger.dataset.index, 10) : 0);
        });
        lightbox.querySelector('[data-lb-prev]')?.addEventListener('click', () => show(current - 1));
        lightbox.querySelector('[data-lb-next]')?.addEventListener('click', () => show(current + 1));
        lightbox.addEventListener('keydown', e => {
            if (e.key === 'ArrowLeft') show(current - 1);
            if (e.key === 'ArrowRight') show(current + 1);
        });
    }

    // ---------- Experience steps -> chip text ----------
    const chip = document.querySelector('[data-story-chip]');
    const steps = document.querySelectorAll('.story-step');
    const activate = (step) => {
        steps.forEach(s => s.classList.toggle('is-active', s === step));
        if (chip) chip.textContent = step.dataset.chip;
    };
    if (steps.length) {
        activate(steps[0]);
        steps.forEach(s => s.addEventListener('mouseenter', () => activate(s)));
    }

    // ---------- Count-up stats ----------
    const counters = document.querySelectorAll('[data-count]');

    // ---------- Scroll reveal + count-up ----------
    if ('IntersectionObserver' in window) {
        const io = new IntersectionObserver((entries) => {
            entries.forEach(entry => {
                if (!entry.isIntersecting) return;
                const el = entry.target;
                if (el.classList.contains('reveal')) el.classList.add('is-visible');
                if (el.dataset.count) countUp(el);
                io.unobserve(el);
            });
        }, { threshold: 0.15 });
        document.querySelectorAll('.reveal').forEach(el => io.observe(el));
        counters.forEach(el => io.observe(el));

        if (steps.length) {
            const stepIo = new IntersectionObserver((entries) => {
                entries.forEach(e => { if (e.isIntersecting) activate(e.target); });
            }, { rootMargin: '-45% 0px -45% 0px' });
            steps.forEach(s => stepIo.observe(s));
        }
    } else {
        document.querySelectorAll('.reveal').forEach(el => el.classList.add('is-visible'));
    }

    function countUp(el) {
        if (window.matchMedia('(prefers-reduced-motion: reduce)').matches) return;
        const target = parseInt(el.dataset.count, 10);
        const suffix = el.dataset.suffix || '';
        const start = performance.now();
        const dur = 1200;
        const tick = (now) => {
            const p = Math.min(1, (now - start) / dur);
            el.textContent = Math.round(target * (1 - Math.pow(1 - p, 3))) + suffix;
            if (p < 1) requestAnimationFrame(tick);
        };
        requestAnimationFrame(tick);
    }
})();
