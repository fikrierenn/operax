/*
 * Operax El-Terminali — tarama/aksiyon geri bildirimi (ux-design-patterns Mobil §M2/M5).
 * 3 kanal: görsel tam-ekran flash + titreşim + kısa bip. Gürültülü depoda tek kanal yetmez.
 *
 * Kullanım:
 *   OperaxTerminal.success("Ürün okundu");   // yeşil flash + tek titreşim + bip
 *   OperaxTerminal.error("Ürün siparişte yok"); // kırmızı flash + çift titreşim + çift bip
 * Otomatik: sayfada <div data-term-feedback="success|error" data-term-msg="..."> varsa
 *   (TempData'dan render) yüklenince tetiklenir.
 */
(function () {
    'use strict';

    var audioCtx = null;
    function beep(freq, dur, count) {
        try {
            audioCtx = audioCtx || new (window.AudioContext || window.webkitAudioContext)();
            for (var i = 0; i < count; i++) {
                var t = audioCtx.currentTime + i * (dur + 0.07);
                var osc = audioCtx.createOscillator(), gain = audioCtx.createGain();
                osc.frequency.value = freq; osc.type = 'square';
                gain.gain.setValueAtTime(0.15, t);
                gain.gain.exponentialRampToValueAtTime(0.001, t + dur);
                osc.connect(gain); gain.connect(audioCtx.destination);
                osc.start(t); osc.stop(t + dur);
            }
        } catch (e) { /* ses engelliyse sessiz geç */ }
    }

    function vibrate(pattern) {
        if (navigator.vibrate) { try { navigator.vibrate(pattern); } catch (e) { } }
    }

    function flash(color, msg) {
        var el = document.createElement('div');
        el.style.cssText = 'position:fixed;inset:0;z-index:9999;display:flex;align-items:center;' +
            'justify-content:center;flex-direction:column;gap:16px;color:#fff;font-weight:800;' +
            'text-align:center;padding:24px;background:' + color + ';opacity:0;transition:opacity .12s;' +
            'pointer-events:none;';
        var icon = document.createElement('div');
        icon.style.cssText = 'font-size:72px;line-height:1';
        icon.textContent = color.indexOf('22c55e') !== -1 || color.indexOf('16a34a') !== -1 ? '✓' : '✕';
        var text = document.createElement('div');
        text.style.cssText = 'font-size:22px';
        text.textContent = msg || '';
        el.appendChild(icon); if (msg) el.appendChild(text);
        document.body.appendChild(el);
        requestAnimationFrame(function () { el.style.opacity = '0.96'; });
        setTimeout(function () {
            el.style.opacity = '0';
            setTimeout(function () { el.remove(); }, 150);
        }, 900);
    }

    window.OperaxTerminal = {
        success: function (msg) { flash('#16a34a', msg || 'Tamam'); vibrate(100); beep(880, 0.08, 1); },
        error: function (msg) { flash('#dc2626', msg || 'Hata'); vibrate([200, 100, 200]); beep(220, 0.12, 2); }
    };

    document.addEventListener('DOMContentLoaded', function () {
        var fb = document.querySelector('[data-term-feedback]');
        if (!fb) return;
        var kind = fb.getAttribute('data-term-feedback');
        var msg = fb.getAttribute('data-term-msg') || '';
        if (kind === 'success') OperaxTerminal.success(msg);
        else if (kind === 'error') OperaxTerminal.error(msg);
    });
})();
