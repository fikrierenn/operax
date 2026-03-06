document.addEventListener('keydown', function (e) {
    // If user is typing in a text area or content editable, do not trigger shortcuts unless explicitly handled
    if (e.target.tagName === 'TEXTAREA' || e.target.isContentEditable) return;

    // Alt+N (New)
    if (e.altKey && e.key.toLowerCase() === 'n') {
        const btn = document.querySelector('[data-shortcut="Alt+N"]');
        if (btn) { e.preventDefault(); btn.click(); }
    }

    // Alt+S (Save/Draft)
    if (e.altKey && e.key.toLowerCase() === 's') {
        const btn = document.querySelector('[data-shortcut="Alt+S"]');
        if (btn) { e.preventDefault(); btn.click(); }
    }

    // Alt+P (Post)
    if (e.altKey && e.key.toLowerCase() === 'p') {
        const btn = document.querySelector('[data-shortcut="Alt+P"]');
        if (btn) { e.preventDefault(); btn.click(); }
    }

    // Alt+C (Cancel)
    if (e.altKey && e.key.toLowerCase() === 'c') {
        const btn = document.querySelector('[data-shortcut="Alt+C"]');
        if (btn) { e.preventDefault(); btn.click(); }
    }

    // F2 (Edit)
    if (e.key === 'F2') {
        const btn = document.querySelector('[data-shortcut="F2"]');
        if (btn) { e.preventDefault(); btn.click(); }
    }

    // Escape (Close modal / Back to list)
    if (e.key === 'Escape') {
        const btn = document.querySelector('[data-shortcut="Escape"]');
        if (btn) { e.preventDefault(); btn.click(); }
    }
});
