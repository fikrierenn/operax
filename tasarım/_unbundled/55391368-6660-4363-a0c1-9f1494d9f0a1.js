/* OPERAX — Pure JS · UI helpers
   h() · el() · esc() · ortak komponentler (badge, kpi, sparkline, vb.) */
(function () {
  const I = window.ICONS;
  const D = window.OPX;

  // HTML escape
  const esc = (s) => String(s == null ? '' : s)
    .replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;')
    .replace(/"/g, '&quot;').replace(/'/g, '&#39;');

  // Tagged template helper — auto-escapes interpolated values unless wrapped in raw()
  const raw = (s) => ({ __raw: String(s) });
  const html = (strings, ...values) => {
    let out = strings[0];
    for (let i = 0; i < values.length; i++) {
      const v = values[i];
      if (v == null || v === false) out += '';
      else if (v && v.__raw) out += v.__raw;
      else if (Array.isArray(v)) out += v.map((x) => (x && x.__raw) ? x.__raw : (typeof x === 'string' && x.startsWith('<') ? x : esc(x))).join('');
      else if (typeof v === 'string' && v.startsWith('<')) out += v; // raw HTML/SVG (icons)
      else out += esc(v);
      out += strings[i + 1];
    }
    return raw(out);
  };

  // Builds a DOM node from an HTML string (or raw obj)
  const node = (s) => {
    const t = document.createElement('template');
    t.innerHTML = (s && s.__raw) ? s.__raw : String(s).trim();
    return t.content.firstElementChild;
  };

  // ---------- Badges ----------
  const statusBadge = (s) => {
    if (s === 'Draft' || s === 'draft') return html`<span class="badge badge-warn"><span class="badge-dot"></span>Taslak</span>`;
    if (s === 'Posted' || s === 'posted') return html`<span class="badge badge-success"><span class="badge-dot"></span>Onaylandı</span>`;
    if (s === 'Cancelled' || s === 'cancelled') return html`<span class="badge badge-danger"><span class="badge-dot"></span>İptal</span>`;
    return html`<span class="badge badge-neutral">${s}</span>`;
  };

  const shipBadge = (s) => {
    if (s === 'shipped')   return html`<span class="badge badge-success"><span class="badge-dot"></span>Sevk Edildi</span>`;
    if (s === 'partial')   return html`<span class="badge badge-warn"><span class="badge-dot"></span>Kısmi Sevk</span>`;
    if (s === 'pending')   return html`<span class="badge badge-info"><span class="badge-dot"></span>Hazırlanıyor</span>`;
    if (s === 'cancelled') return html`<span class="badge badge-danger"><span class="badge-dot"></span>İptal</span>`;
    return html`<span class="badge badge-neutral">${s}</span>`;
  };

  const movementBadge = (t) => {
    const map = { in: 'success', out: 'info', tr: 'brand', cnt: 'warn', adj: 'neutral', ret: 'danger' };
    return html`<span class="badge badge-${map[t.k] || 'neutral'}"><span class="badge-dot"></span>${t.label}</span>`;
  };

  const woStatusBadge = (s) => {
    if (s === 'Planned')     return html`<span class="badge badge-neutral"><span class="badge-dot"></span>Planlandı</span>`;
    if (s === 'Released')    return html`<span class="badge badge-info"><span class="badge-dot"></span>Açıldı</span>`;
    if (s === 'InProgress')  return html`<span class="badge badge-warn"><span class="badge-dot"></span>Üretimde</span>`;
    if (s === 'Completed')   return html`<span class="badge badge-success"><span class="badge-dot"></span>Tamamlandı</span>`;
    if (s === 'Cancelled')   return html`<span class="badge badge-danger"><span class="badge-dot"></span>İptal</span>`;
    return html`<span class="badge badge-neutral">${s}</span>`;
  };

  const priorityBadge = (p) => {
    if (p === 'urgent') return html`<span class="badge badge-danger"><span class="badge-dot"></span>Acil</span>`;
    if (p === 'high')   return html`<span class="badge badge-warn"><span class="badge-dot"></span>Yüksek</span>`;
    if (p === 'low')    return html`<span class="badge badge-neutral"><span class="badge-dot"></span>Düşük</span>`;
    return html`<span class="badge badge-info"><span class="badge-dot"></span>Normal</span>`;
  };

  // ---------- Avatars ----------
  const avatar = (text, opts = {}) => {
    const { size = 28, fontSize, color, gradient } = opts;
    const bg = gradient || color || (() => {
      const c = (text || '?').charCodeAt(0) * 17;
      return `linear-gradient(135deg, hsl(${c % 360} 65% 52%), hsl(${(c + 50) % 360} 65% 48%))`;
    })();
    const fs = fontSize || Math.max(9, Math.floor(size * 0.38));
    return html`<div class="avatar" style="width:${size}px;height:${size}px;font-size:${fs}px;background:${raw(bg)}">${text}</div>`;
  };

  const initials = (name) => (name || '').split(' ').map((w) => w[0]).join('').slice(0, 2);

  // ---------- Breadcrumb ----------
  const breadcrumb = (items) => {
    const parts = [];
    items.forEach((it, i) => {
      const isLast = i === items.length - 1;
      const cls = isLast ? 'breadcrumb-current' : 'breadcrumb-item';
      const dataAttr = !isLast && it.route ? ` data-action="nav" data-route="${esc(it.route)}"` : '';
      const monoCls = it.mono ? ' mono' : '';
      parts.push(`<span class="${cls}${monoCls}"${dataAttr}>${esc(it.label)}</span>`);
      if (!isLast) parts.push(`<span class="breadcrumb-sep">${I.ChevronRight(11, 2)}</span>`);
    });
    return html`<div class="breadcrumb">${raw(parts.join(''))}</div>`;
  };

  // ---------- Page header ----------
  const pageHeader = ({ crumbs, title, sub, titleSlot, actions }) => {
    return html`
      <div class="page-hdr">
        <div class="page-hdr-l">
          ${breadcrumb(crumbs)}
          ${titleSlot ? titleSlot : html`<h1>${title}</h1>`}
          ${sub ? html`<p>${raw(sub)}</p>` : ''}
        </div>
        <div class="page-hdr-r">${raw(actions || '')}</div>
      </div>
    `;
  };

  // ---------- KPI ----------
  const kpiCard = ({ label, value, unit, glow = 'brand', sub, valueSize = 24, valueColor }) => {
    const style = valueColor ? ` style="font-size:${valueSize}px;color:${valueColor}"` : ` style="font-size:${valueSize}px"`;
    return html`
      <div class="kpi">
        <div class="kpi-glow ${raw(glow)}"></div>
        <div class="kpi-label">${label}</div>
        <div class="kpi-value"${raw(style)}>${value}${unit ? html`<span class="kpi-unit">${unit}</span>` : ''}</div>
        ${sub ? html`<div class="kpi-trend" style="margin-top:8px">${raw(sub)}</div>` : ''}
      </div>
    `;
  };

  // ---------- Sparkline (SVG string) ----------
  const sparkline = (data, opts = {}) => {
    const { color = 'var(--brand-500)', w = 140, h = 36, fill = true } = opts;
    const max = Math.max(...data), min = Math.min(...data);
    const range = max - min || 1;
    const pts = data.map((v, i) => {
      const x = (i / (data.length - 1)) * w;
      const y = h - 4 - ((v - min) / range) * (h - 8);
      return [x, y];
    });
    const path = pts.map(([x, y], i) => `${i === 0 ? 'M' : 'L'}${x.toFixed(1)},${y.toFixed(1)}`).join(' ');
    const area = `${path} L${w},${h} L0,${h} Z`;
    const gid = 'sg-' + Math.random().toString(36).slice(2, 8);
    return `
      <svg width="100%" height="${h}" viewBox="0 0 ${w} ${h}" preserveAspectRatio="none">
        <defs>
          <linearGradient id="${gid}" x1="0" y1="0" x2="0" y2="1">
            <stop offset="0%" stop-color="${color}" stop-opacity="0.28"/>
            <stop offset="100%" stop-color="${color}" stop-opacity="0"/>
          </linearGradient>
        </defs>
        ${fill ? `<path d="${area}" fill="url(#${gid})"/>` : ''}
        <path d="${path}" fill="none" stroke="${color}" stroke-width="1.6" stroke-linecap="round" stroke-linejoin="round"/>
        <circle cx="${pts[pts.length-1][0]}" cy="${pts[pts.length-1][1]}" r="2.5" fill="${color}"/>
      </svg>
    `;
  };

  // ---------- Gauge / progress ----------
  const gauge = (pct, height = 8) => {
    const p = Math.max(0, Math.min(100, pct));
    const color = pct < 25 ? 'var(--danger)' : pct < 50 ? 'var(--warn)' : pct < 90 ? 'var(--success)' : 'var(--brand-500)';
    return `
      <div style="width:100%;height:${height}px;background:var(--bg-2);border-radius:${height/2}px;overflow:hidden">
        <div style="width:${p}%;height:100%;background:${color};border-radius:${height/2}px;transition:width .4s"></div>
      </div>
    `;
  };

  // ---------- Empty state ----------
  const emptyState = ({ icon = 'Box', title, msg, action }) => {
    return html`
      <div class="empty-state" style="padding:80px 32px">
        <div class="empty-state-icon">${raw(I[icon](32, 2))}</div>
        <h3>${title}</h3>
        ${msg ? html`<p>${msg}</p>` : ''}
        ${action ? raw(action) : ''}
      </div>
    `;
  };

  // ---------- Tabs ----------
  const tabs = (items, active, group = 'tab') => {
    const buttons = items.map((it) => `
      <div class="tab${active === it.id ? ' active' : ''}" data-action="${esc(group)}" data-tab="${esc(it.id)}">
        ${it.icon ? I[it.icon](14, 2) : ''}
        ${esc(it.label)}
        ${it.count != null ? `<span class="tab-count">${esc(it.count)}</span>` : ''}
      </div>
    `).join('');
    return html`<div class="tabs">${raw(buttons)}</div>`;
  };

  // ---------- Status flow (Draft → Posted → Cancelled) ----------
  const statusFlow = (status, dates) => {
    const isDraft = status === 'Draft';
    const isPosted = status === 'Posted';
    const isCancelled = status === 'Cancelled';
    const draftDone = isPosted || isCancelled;
    const draftCurrent = isDraft;
    const postedDone = isPosted;
    const postedSkipped = isCancelled && !dates.posted;
    const showCancelStep = isCancelled;
    let h = `<div class="status-flow">`;
    h += `
      <div class="status-step done">
        <div class="status-dot">${I.Plus(12, 3)}</div>
        <div>
          <div class="status-step-label">Oluşturuldu</div>
          <div class="status-step-time">${esc(dates.created)}</div>
        </div>
      </div>
      <div class="status-bar ${draftDone ? 'filled' : draftCurrent ? 'filled-current' : ''}"></div>
      <div class="status-step ${draftDone ? 'done' : draftCurrent ? 'current' : ''}">
        <div class="status-dot">${I.Edit(11, 2.6)}</div>
        <div>
          <div class="status-step-label">Taslak</div>
          <div class="status-step-time">${esc(dates.draft || '—')}</div>
        </div>
      </div>
      <div class="status-bar ${postedDone ? 'filled' : draftDone && isPosted ? 'filled-current' : isCancelled ? 'filled-cancel' : ''}"></div>
    `;
    if (isCancelled && !dates.posted) {
      h += `
        <div class="status-step cancelled">
          <div class="status-dot">${I.X(11, 3)}</div>
          <div>
            <div class="status-step-label">İptal Edildi</div>
            <div class="status-step-time">${esc(dates.cancelled)}</div>
          </div>
        </div>
      `;
    } else {
      h += `
        <div class="status-step ${postedDone ? 'done' : ''} ${postedSkipped ? 'cancelled' : ''}">
          <div class="status-dot">${postedSkipped ? I.X(11, 3) : I.Check(12, 3)}</div>
          <div>
            <div class="status-step-label">Onaylandı</div>
            <div class="status-step-time">${esc(dates.posted || '—')}</div>
          </div>
        </div>
      `;
      if (isCancelled && dates.posted) {
        h += `
          <div class="status-bar filled-cancel"></div>
          <div class="status-step cancelled">
            <div class="status-dot">${I.X(11, 3)}</div>
            <div>
              <div class="status-step-label">İptal Edildi</div>
              <div class="status-step-time">${esc(dates.cancelled)}</div>
            </div>
          </div>
        `;
      }
    }
    h += `</div>`;
    return raw(h);
  };

  // ---------- Button helpers ----------
  const btn = (label, opts = {}) => {
    const { kind = 'primary', size = 'sm', icon, action, route, kbd, dataset = {} } = opts;
    const ds = Object.entries(dataset).map(([k, v]) => `data-${k}="${esc(v)}"`).join(' ');
    return `
      <button class="btn btn-${kind} btn-${size}" ${action ? `data-action="${esc(action)}"` : ''} ${route ? `data-route="${esc(route)}"` : ''} ${ds}>
        ${icon ? I[icon](size === 'xs' ? 12 : size === 'sm' ? 13 : 14, 2) : ''}
        ${esc(label)}
        ${kbd ? `<span class="btn-kbd">${esc(kbd)}</span>` : ''}
      </button>
    `;
  };

  window.UI = {
    esc, raw, html, node,
    statusBadge, shipBadge, movementBadge, woStatusBadge, priorityBadge,
    avatar, initials,
    breadcrumb, pageHeader,
    kpiCard, sparkline, gauge, emptyState, tabs, statusFlow, btn,
  };
})();
