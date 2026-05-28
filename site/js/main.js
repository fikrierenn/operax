/* Operax kurumsal site — etkileşim scriptleri */
(function () {
    "use strict";

    // ── Nav: scroll gölgesi + mobil menü ──
    const nav = document.getElementById("nav");
    const toggle = document.getElementById("navToggle");
    const links = document.getElementById("navLinks");

    const onScroll = () => nav.classList.toggle("scrolled", window.scrollY > 10);
    window.addEventListener("scroll", onScroll, { passive: true });
    onScroll();

    toggle?.addEventListener("click", () => links.classList.toggle("open"));
    links?.querySelectorAll("a").forEach(a =>
        a.addEventListener("click", () => links.classList.remove("open"))
    );

    // ── Scroll reveal (IntersectionObserver) ──
    const io = new IntersectionObserver(
        entries => entries.forEach(e => {
            if (e.isIntersecting) { e.target.classList.add("in"); io.unobserve(e.target); }
        }),
        { threshold: 0.12 }
    );
    document.querySelectorAll(".reveal").forEach(el => io.observe(el));

    // ── Sayaç animasyonu ──
    const animateCount = el => {
        const target = parseInt(el.dataset.count, 10);
        const suffix = el.dataset.suffix || "";
        const dur = 1400, t0 = performance.now();
        const tick = now => {
            const p = Math.min((now - t0) / dur, 1);
            const eased = 1 - Math.pow(1 - p, 3);
            el.textContent = (suffix === "/7" ? 24 : Math.round(target * eased)) + suffix;
            if (p < 1) requestAnimationFrame(tick);
            else el.textContent = (suffix === "/7" ? 24 : target) + suffix;
        };
        requestAnimationFrame(tick);
    };
    const statIo = new IntersectionObserver(
        entries => entries.forEach(e => {
            if (e.isIntersecting) {
                const b = e.target.querySelector("b[data-count]");
                if (b) animateCount(b);
                statIo.unobserve(e.target);
            }
        }),
        { threshold: 0.5 }
    );
    document.querySelectorAll(".stat").forEach(el => statIo.observe(el));

    // ── Ekran sekmeleri ──
    const tabs = document.querySelectorAll(".screen-tabs .tab");
    const screens = document.querySelectorAll(".screen-stage .screen");
    tabs.forEach(tab => tab.addEventListener("click", () => {
        const key = tab.dataset.screen;
        tabs.forEach(t => t.classList.toggle("active", t === tab));
        screens.forEach(s => s.classList.toggle("active", s.dataset.screen === key));
    }));

    // ── Demo formu (statik: gerçek gönderim yok) ──
    const form = document.getElementById("demoForm");
    const note = document.getElementById("formNote");
    form?.addEventListener("submit", e => {
        e.preventDefault();
        if (!form.checkValidity()) {
            note.textContent = "Lütfen tüm alanları doldurun.";
            return;
        }
        const ad = form.elements["ad"].value.trim();
        note.textContent = `Teşekkürler ${ad}! Talebiniz alındı, en kısa sürede dönüş yapacağız.`;
        form.reset();
    });
})();
