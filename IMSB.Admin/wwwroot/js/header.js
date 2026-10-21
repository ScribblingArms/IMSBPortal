// Header search (/Search/Query) and notifications (/Notifications/Data). Both panels are left out of pages that
// set ViewData["HideHeaderTools"], so everything here checks that its elements exist.
// Localized strings come from window.IMSBHeaderI18n (Views/Shared/_Header.cshtml).
(function HeaderModule(){
  const I18N = window.IMSBHeaderI18n || {};
  function t(key, ...args){
    let s = I18N[key] || key;
    args.forEach((a, i) => { s = s.split('{' + i + '}').join(a); });
    return s;
  }
  function esc(v){
    return String(v == null ? '' : v).replace(/[&<>"']/g, c => ({ '&':'&amp;', '<':'&lt;', '>':'&gt;', '"':'&quot;', "'":'&#39;' }[c]));
  }
  const $ = id => document.getElementById(id);
  const MIN = 2;
  const SEEN_KEY = 'imsbSeenNotifications';
  const REFRESH_MS = 2 * 60 * 1000;

  // ── Panels ────────────────────────────────────────────────────
  function setOpen(btn, panel, open){
    panel.hidden = !open;
    btn.setAttribute('aria-expanded', String(open));
  }
  function closeAll(except){
    [['headerSearchBtn', 'headerSearchPanel'], ['headerNotifBtn', 'headerNotifPanel']].forEach(([b, p]) => {
      if (p !== except && $(p)) setOpen($(b), $(p), false);
    });
  }

  // ── Search ────────────────────────────────────────────────────
  const GROUP_LABEL = {
    PAGES: 'SearchGroupPages', ARTISANS: 'SearchGroupArtisans', APPLICATIONS: 'SearchGroupApplications',
    STALLS: 'SearchGroupStalls', STAFF: 'SearchGroupStaff', PRODUCTS: 'SearchGroupProducts'
  };
  let searchSeq = 0;
  let searchTimer = null;

  function renderSearch(groups){
    const box = $('headerSearchResults');
    if (!groups.length){ box.innerHTML = `<div class="header-pop-empty">${esc(t('SearchNoResults'))}</div>`; return; }
    box.innerHTML = groups.map(g => `
      <div class="header-pop-group">${esc(t(GROUP_LABEL[g.type] || g.type))}</div>
      ${g.items.map(i => `<a class="header-pop-item" href="${esc(i.url)}">
          <i class="fa-solid ${esc(g.icon)}"></i>
          <span><span class="header-pop-title">${esc(i.title)}</span>${i.subtitle ? `<span class="header-pop-sub">${esc(i.subtitle)}</span>` : ''}</span>
        </a>`).join('')}`).join('');
  }

  async function search(){
    const q = $('headerSearchInput').value.trim();
    const box = $('headerSearchResults');
    const seq = ++searchSeq;
    if (q.length < MIN){ box.innerHTML = `<div class="header-pop-empty">${esc(t('SearchMinChars'))}</div>`; return; }
    box.innerHTML = `<div class="header-pop-empty">${esc(t('Searching'))}</div>`;
    try {
      const res = await fetch('/Search/Query?q=' + encodeURIComponent(q));
      if (seq !== searchSeq) return;
      if (!res.ok){ box.innerHTML = `<div class="header-pop-empty">${esc(t('CouldNotReachServer'))}</div>`; return; }
      renderSearch((await res.json()).groups || []);
    } catch (e) {
      console.error(e);
      if (seq === searchSeq) box.innerHTML = `<div class="header-pop-empty">${esc(t('CouldNotReachServer'))}</div>`;
    }
  }

  function initSearch(){
    const btn = $('headerSearchBtn'), panel = $('headerSearchPanel'), input = $('headerSearchInput');
    if (!btn || !panel || !input) return;
    const open = () => { closeAll(panel.id); setOpen(btn, panel, true); input.focus(); input.select(); };
    btn.addEventListener('click', e => { e.stopPropagation(); panel.hidden ? open() : setOpen(btn, panel, false); });
    input.addEventListener('input', () => { clearTimeout(searchTimer); searchTimer = setTimeout(search, 250); });
    input.addEventListener('keydown', e => {
      const links = [...panel.querySelectorAll('.header-pop-item')];
      if (e.key === 'Enter' && links.length){ e.preventDefault(); window.location.href = links[0].getAttribute('href'); }
      else if (e.key === 'ArrowDown' && links.length){ e.preventDefault(); links[0].focus(); }
    });
    panel.addEventListener('keydown', e => {
      const links = [...panel.querySelectorAll('.header-pop-item')];
      const i = links.indexOf(document.activeElement);
      if (i < 0) return;
      if (e.key === 'ArrowDown'){ e.preventDefault(); (links[i + 1] || links[i]).focus(); }
      else if (e.key === 'ArrowUp'){ e.preventDefault(); (i ? links[i - 1] : input).focus(); }
    });
    // Ctrl+K / Cmd+K, or "/" outside a text field, opens the search
    document.addEventListener('keydown', e => {
      const typing = /^(INPUT|TEXTAREA|SELECT)$/.test(document.activeElement?.tagName || '') || document.activeElement?.isContentEditable;
      if (((e.ctrlKey || e.metaKey) && e.key.toLowerCase() === 'k') || (e.key === '/' && !typing)){ e.preventDefault(); open(); }
    });
  }

  // ── Notifications ─────────────────────────────────────────────
  const NOTIF = {
    APPS_PENDING: ['NotifAppsPending', 'fa-inbox'],
    APPS_REVIEW: ['NotifAppsReview', 'fa-magnifying-glass'],
    REALLOCATIONS_PENDING: ['NotifReallocationsPending', 'fa-right-left'],
    AGREEMENTS_EXPIRED: ['NotifAgreementsExpired', 'fa-file-circle-xmark'],
    AGREEMENTS_EXPIRING: ['NotifAgreementsExpiring', 'fa-file-contract'],
    ARTISAN_RENEWALS: ['NotifArtisanRenewals', 'fa-rotate'],
    OUT_OF_STOCK: ['NotifOutOfStock', 'fa-box-open'],
    LOW_STOCK: ['NotifLowStock', 'fa-triangle-exclamation'],
    CONTRACTS_EXPIRING: ['NotifContractsExpiring', 'fa-user-clock']
  };
  let items = [];

  function seen(){ try { return new Set(JSON.parse(localStorage.getItem(SEEN_KEY) || '[]')); } catch { return new Set(); } }
  function markSeen(ids){ try { localStorage.setItem(SEEN_KEY, JSON.stringify(ids)); } catch { /* private mode: dot stays */ } }

  function renderNotifications(){
    const list = $('headerNotifList'), dot = $('headerNotifDot');
    const read = seen();
    const unread = items.filter(n => !read.has(n.id)).length;
    dot.hidden = unread === 0;
    $('headerNotifBtn').setAttribute('data-unread', String(unread));
    if (!items.length){ list.innerHTML = `<div class="header-pop-empty">${esc(t('NoNotifications'))}</div>`; return; }
    list.innerHTML = items.map(n => {
      const [key, icon] = NOTIF[n.type] || [n.type, 'fa-bell'];
      return `<a class="header-pop-item notif-${esc(n.severity)}${read.has(n.id) ? '' : ' unread'}" href="${esc(n.url)}">
        <i class="fa-solid ${icon}"></i><span class="header-pop-title">${esc(t(key, n.count))}</span></a>`;
    }).join('');
  }

  async function loadNotifications(){
    try {
      const res = await fetch('/Notifications/Data');
      if (!res.ok) return;
      items = (await res.json()).items || [];
      renderNotifications();
    } catch (e) { console.warn('Notifications unavailable', e); }
  }

  function initNotifications(){
    const btn = $('headerNotifBtn'), panel = $('headerNotifPanel');
    if (!btn || !panel) return;
    btn.addEventListener('click', e => {
      e.stopPropagation();
      const open = panel.hidden;
      closeAll(panel.id);
      setOpen(btn, panel, open);
      if (open) loadNotifications();
    });
    $('headerNotifMarkRead').addEventListener('click', () => { markSeen(items.map(n => n.id)); renderNotifications(); });
    loadNotifications();
    setInterval(() => { if (!document.hidden) loadNotifications(); }, REFRESH_MS);
  }

  document.addEventListener('DOMContentLoaded', () => {
    initSearch();
    initNotifications();
    document.addEventListener('click', e => { if (!e.target.closest('.header-pop-wrap')) closeAll(); });
    document.addEventListener('keydown', e => { if (e.key === 'Escape') closeAll(); });
  });
})();
