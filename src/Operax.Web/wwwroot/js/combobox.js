/*
 * Operax Aranabilir Combobox — progressive enhancement.
 * Kullanım: <select data-combobox> ... </select>
 * Mevcut <select> gizlenir, üstüne arama input'u + filtreli açılır liste konur.
 * Seçimde underlying <select>.value set edilir + 'change' event fırlatılır
 * (mevcut otomatik-doldurma JS'leri çalışmaya devam eder). Form binding DEĞİŞMEZ.
 * Klavye: yaz=filtrele, ↑↓=gez, Enter=seç, Esc=kapat, Tab=uygula.
 * Stil JS ile (CSS değişkenleri ile tema uyumlu) — .cshtml inline-style kuralı dışında.
 */
(function () {
    'use strict';

    function enhance(select) {
        if (select.dataset.cbReady) return;
        select.dataset.cbReady = '1';

        var options = Array.prototype.slice.call(select.options);
        var placeholder = select.options.length && !select.options[0].value
            ? select.options[0].textContent.trim() : 'Ara / seç…';

        // Sarmalayıcı
        var wrap = document.createElement('div');
        wrap.style.position = 'relative';
        select.parentNode.insertBefore(wrap, select);
        select.style.display = 'none';
        wrap.appendChild(select);

        // Görünür arama input'u
        var input = document.createElement('input');
        input.type = 'text';
        input.className = select.className.replace('form-ctrl', 'form-ctrl') || 'form-ctrl';
        input.setAttribute('role', 'combobox');
        input.setAttribute('aria-expanded', 'false');
        input.setAttribute('autocomplete', 'off');
        input.placeholder = placeholder;
        if (select.required) input.required = true;
        // Seçili değer varsa metnini göster
        var sel = select.options[select.selectedIndex];
        if (sel && sel.value) input.value = sel.textContent.trim();
        wrap.appendChild(input);

        // Açılır liste
        var list = document.createElement('div');
        list.style.cssText = 'position:absolute;left:0;right:0;top:100%;z-index:70;margin-top:2px;' +
            'background:var(--surface,#fff);border:1px solid var(--border,#e2e8f0);border-radius:8px;' +
            'box-shadow:var(--shadow,0 8px 24px rgba(0,0,0,.12));max-height:280px;overflow-y:auto;display:none;';
        wrap.appendChild(list);

        var active = -1, filtered = [];

        function render(term) {
            term = (term || '').toLocaleLowerCase('tr');
            list.innerHTML = '';
            filtered = options.filter(function (o) {
                if (!o.value) return false; // placeholder atla
                return !term || o.textContent.toLocaleLowerCase('tr').indexOf(term) !== -1;
            }).slice(0, 50); // performans: ilk 50
            if (!filtered.length) {
                var none = document.createElement('div');
                none.textContent = 'Eşleşme yok';
                none.style.cssText = 'padding:9px 12px;color:var(--text-3,#94a3b8);font-size:13px;';
                list.appendChild(none);
                return;
            }
            filtered.forEach(function (o, i) {
                var row = document.createElement('div');
                row.textContent = o.textContent.trim();
                row.dataset.value = o.value;
                row.style.cssText = 'padding:9px 12px;cursor:pointer;font-size:13px;color:var(--text,#0f172a);';
                row.addEventListener('mousedown', function (e) { e.preventDefault(); pick(o); });
                row.addEventListener('mouseenter', function () { setActive(i); });
                list.appendChild(row);
            });
            setActive(filtered.length ? 0 : -1);
        }

        function setActive(i) {
            active = i;
            Array.prototype.forEach.call(list.children, function (c, idx) {
                c.style.background = idx === i ? 'var(--brand-tint-15,#eef2ff)' : 'transparent';
            });
            if (i >= 0 && list.children[i]) list.children[i].scrollIntoView({ block: 'nearest' });
        }

        function open() { render(''); list.style.display = 'block'; input.setAttribute('aria-expanded', 'true'); }
        function close() { list.style.display = 'none'; input.setAttribute('aria-expanded', 'false'); }

        function pick(o) {
            select.value = o.value;
            input.value = o.textContent.trim();
            select.dispatchEvent(new Event('change', { bubbles: true }));
            close();
        }

        input.addEventListener('focus', open);
        input.addEventListener('input', function () {
            // metin değişince seçim geçersiz — boşalt (yanlış eşleşmeyi önle)
            select.value = '';
            render(input.value);
            list.style.display = 'block';
        });
        input.addEventListener('keydown', function (e) {
            if (e.key === 'ArrowDown') { e.preventDefault(); if (list.style.display === 'none') open(); setActive(Math.min(active + 1, filtered.length - 1)); }
            else if (e.key === 'ArrowUp') { e.preventDefault(); setActive(Math.max(active - 1, 0)); }
            else if (e.key === 'Enter') { if (list.style.display !== 'none' && filtered[active]) { e.preventDefault(); pick(filtered[active]); } }
            else if (e.key === 'Escape') { close(); }
        });
        input.addEventListener('blur', function () {
            setTimeout(function () {
                close();
                // Geçerli seçim yoksa input'u temizle (yarım yazım kalmasın)
                if (!select.value) input.value = '';
            }, 150);
        });
    }

    function init(root) {
        (root || document).querySelectorAll('select[data-combobox]').forEach(enhance);
    }

    document.addEventListener('DOMContentLoaded', function () { init(document); });
    // Modal/dinamik içerik için dışarı aç
    window.OperaxCombobox = { init: init, enhance: enhance };
})();
