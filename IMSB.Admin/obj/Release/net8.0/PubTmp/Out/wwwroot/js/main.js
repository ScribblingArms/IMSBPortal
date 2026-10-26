/* ============================================================
   IMSB Admin Portal – main.js
   Shared utilities: sidebar, dark mode, toasts, modals
   ============================================================ */

/* ── i18n ────────────────────────────────────────────────────── */
// Common strings rendered by _Layout.cshtml (window.IMSBI18n, from SharedResource.<culture>.resx)
function IMSBT(key, ...args) {
  let s = (window.IMSBI18n && window.IMSBI18n[key]) || key;
  args.forEach((a, i) => { s = s.split('{' + i + '}').join(a); });
  return s;
}

/* ── Sidebar Toggle ──────────────────────────────────────────── */
function initSidebar() {
  const sidebar = document.getElementById('sidebar');
  const toggleBtn = document.getElementById('sidebarToggle');
  if (!sidebar || !toggleBtn) return;

  const stored = localStorage.getItem('sidebarCollapsed');
  if (stored === 'true') sidebar.classList.add('collapsed');

  toggleBtn.addEventListener('click', () => {
    if (window.innerWidth <= 700) {
      sidebar.classList.toggle('mobile-open');
    } else {
      sidebar.classList.toggle('collapsed');
      localStorage.setItem('sidebarCollapsed', sidebar.classList.contains('collapsed'));
    }
  });

  // Close mobile sidebar when clicking outside
  document.addEventListener('click', e => {
    if (window.innerWidth <= 700 && !sidebar.contains(e.target) && e.target !== toggleBtn) {
      sidebar.classList.remove('mobile-open');
    }
  });
}

/* ── Dark Mode ───────────────────────────────────────────────── */
function initDarkMode() {
  const btn = document.getElementById('darkModeToggle');
  if (!btn) return;
  const body = document.body;
  const icon = btn.querySelector('i');

  const isDark = localStorage.getItem('darkMode') === 'true';
  if (isDark) {
    body.classList.add('dark-mode');
    if (icon) { icon.classList.remove('fa-moon'); icon.classList.add('fa-sun'); }
  }

  btn.addEventListener('click', () => {
    body.classList.toggle('dark-mode');
    const dark = body.classList.contains('dark-mode');
    localStorage.setItem('darkMode', dark);
    if (icon) {
      icon.classList.toggle('fa-moon', !dark);
      icon.classList.toggle('fa-sun',  dark);
    }
  });
}

/* ── Toast Notifications ─────────────────────────────────────── */
function showToast(title, message, type = 'info', duration = 4000) {
  let container = document.getElementById('toast-container');
  if (!container) {
    container = document.createElement('div');
    container.id = 'toast-container';
    document.body.appendChild(container);
  }

  const icons = { success: 'fa-circle-check', warning: 'fa-triangle-exclamation', error: 'fa-circle-xmark', info: 'fa-circle-info' };
  const toast = document.createElement('div');
  toast.className = `toast ${type}`;
  toast.innerHTML = `
    <i class="fa-solid ${icons[type] || icons.info} toast-icon"></i>
    <div class="toast-content">
      <div class="toast-title">${title}</div>
      ${message ? `<div class="toast-msg">${message}</div>` : ''}
    </div>
    <button class="toast-close" onclick="dismissToast(this.closest('.toast'))"><i class="fa-solid fa-xmark"></i></button>
  `;
  container.appendChild(toast);

  const timer = setTimeout(() => dismissToast(toast), duration);
  toast._timer = timer;
  return toast;
}

function dismissToast(toast) {
  if (!toast || toast.classList.contains('hiding')) return;
  clearTimeout(toast._timer);
  toast.classList.add('hiding');
  setTimeout(() => toast.remove(), 300);
}

/* ── Modal ───────────────────────────────────────────────────── */
function openModal(id) {
  const overlay = document.getElementById(id);
  if (overlay) overlay.classList.add('open');
}
function closeModal(id) {
  const overlay = typeof id === 'string' ? document.getElementById(id) : id;
  if (overlay) overlay.classList.remove('open');
}

function initModals() {
  document.querySelectorAll('.modal-overlay').forEach(overlay => {
    overlay.addEventListener('click', e => {
      if (e.target === overlay) closeModal(overlay);
    });
  });
  document.querySelectorAll('[data-modal-close]').forEach(btn => {
    btn.addEventListener('click', () => {
      const overlay = btn.closest('.modal-overlay');
      if (overlay) closeModal(overlay);
    });
  });
  document.querySelectorAll('[data-modal-open]').forEach(btn => {
    btn.addEventListener('click', () => openModal(btn.dataset.modalOpen));
  });
  document.addEventListener('keydown', e => {
    if (e.key === 'Escape') {
      document.querySelectorAll('.modal-overlay.open').forEach(closeModal);
    }
  });
}

/* ── Tabs ────────────────────────────────────────────────────── */
function initTabs() {
  document.querySelectorAll('.tab-bar').forEach(bar => {
    bar.querySelectorAll('.tab-item').forEach(tab => {
      tab.addEventListener('click', () => {
        const target = tab.dataset.tab;
        const container = bar.closest('[data-tabs]') || bar.parentElement;

        bar.querySelectorAll('.tab-item').forEach(t => t.classList.remove('active'));
        tab.classList.add('active');

        container.querySelectorAll('.tab-panel').forEach(p => p.classList.remove('active'));
        const panel = container.querySelector(`[data-panel="${target}"]`);
        if (panel) panel.classList.add('active');
      });
    });
  });
}

/* ── Form Validation ─────────────────────────────────────────── */
function validateForm(formEl) {
  let valid = true;
  formEl.querySelectorAll('[required]').forEach(field => {
    const group = field.closest('.form-group');
    if (!field.value.trim()) {
      valid = false;
      if (group) group.classList.add('has-error');
      field.classList.add('is-invalid');
    } else {
      if (group) group.classList.remove('has-error');
      field.classList.remove('is-invalid');
    }
  });
  return valid;
}

function initForms() {
  document.querySelectorAll('form[data-validate]').forEach(form => {
    form.addEventListener('submit', e => {
      e.preventDefault();
      if (validateForm(form)) {
        const action = form.dataset.action || 'save';
        showToast(IMSBT('Success'), form.dataset.successMsg || IMSBT('RecordSaved'), 'success');
        if (form.dataset.reset === 'true') form.reset();
      } else {
        showToast(IMSBT('ValidationError'), IMSBT('FillRequiredFields'), 'error');
      }
    });
  });

  // Clear error on input
  document.querySelectorAll('.form-control').forEach(field => {
    field.addEventListener('input', () => {
      field.classList.remove('is-invalid');
      const group = field.closest('.form-group');
      if (group) group.classList.remove('has-error');
    });
  });
}

/* ── Table Search ────────────────────────────────────────────── */
function initTableSearch() {
  document.querySelectorAll('[data-search-table]').forEach(input => {
    const tableId = input.dataset.searchTable;
    const table = document.getElementById(tableId);
    if (!table) return;
    input.addEventListener('input', () => {
      const q = input.value.toLowerCase().trim();
      table.querySelectorAll('tbody tr').forEach(row => {
        row.style.display = row.textContent.toLowerCase().includes(q) ? '' : 'none';
      });
    });
  });
}

/* ── ?q= from the header search ──────────────────────────────── */
// A search result links to e.g. /Stalls/Index?q=A-01; the page's own search box ([data-url-search]) starts with
// that text. This runs before the page scripts' DOMContentLoaded handlers, so their first load is already filtered.
function initUrlSearch() {
  const q = new URLSearchParams(window.location.search).get('q');
  if (!q) return;
  document.querySelectorAll('input[data-url-search]').forEach(input => { input.value = q; });
}

/* ── Active Nav Item ─────────────────────────────────────────── */
function setActiveNav() {
  // match on the controller segment, so /Staff, /Staff/Index and /Staff/Index?x=1 all highlight "/Staff/Index"
  const controller = (window.location.pathname.split('/').filter(Boolean)[0] || '').toLowerCase();
  document.querySelectorAll('.nav-item[data-page]').forEach(item => {
    const page = (item.dataset.page.split('/').filter(Boolean)[0] || '').toLowerCase();
    if (controller && page === controller) {
      item.classList.add('active');
    } else {
      item.classList.remove('active');
    }
  });
}

/* ── Export Buttons ──────────────────────────────────────────── */
// data-export="/Some/Export?x=1" downloads that file; a bare data-export belongs to a module
// that has no backend yet, so it only says so instead of pretending a file was downloaded.
function initExport() {
  document.querySelectorAll('[data-export]').forEach(btn => {
    btn.addEventListener('click', () => {
      const url = btn.dataset.export;
      if (url) { window.location.href = url; return; }
      showToast(IMSBT('ExportUnavailable'), IMSBT('ExportUnavailableMsg'), 'info');
    });
  });
}

/* ── Session timeout ─────────────────────────────────────────── */
// The server ends a session after IdleTimeoutMinutes without requests (_Layout puts the limit on <body>).
// User activity in any tab keeps it alive (one keep-alive per minute at most); shortly before the limit a
// warning offers "Stay signed in"; when it passes the page signs out. Script requests that come back 401
// (session ended elsewhere) go to the login page.
function initSession() {
  const body = document.body;
  const idle = parseInt(body.dataset.sessionIdle || '0', 10) * 1000;
  const warn = parseInt(body.dataset.sessionWarn || '120', 10) * 1000;
  const KEY = 'imsbLastActivity';
  const PING_EVERY = 60 * 1000;
  let lastPing = Date.now();
  let warned = false;
  let ended = false;

  const goToLogin = () => {
    if (ended) return;
    ended = true;
    window.location.href = '/Account/Login?reason=expired&ReturnUrl=' + encodeURIComponent(location.pathname + location.search);
  };

  // any script request that finds the session gone
  const nativeFetch = window.fetch.bind(window);
  window.fetch = async (...args) => {
    const res = await nativeFetch(...args);
    if (res.status === 401 && res.headers.get('X-Session-Ended')) goToLogin();
    return res;
  };

  if (!idle || !document.getElementById('sessionWarningModal')) return;

  const read = () => { try { return parseInt(localStorage.getItem(KEY) || '0', 10) || 0; } catch { return 0; } };
  const write = t => { try { localStorage.setItem(KEY, String(t)); } catch { /* private mode: this tab only */ } };
  let lastActivity = Date.now();
  write(lastActivity);
  const latest = () => Math.max(lastActivity, read());

  const token = () => document.querySelector('#sessionLogoutForm input[name="__RequestVerificationToken"]')?.value || '';
  async function keepAlive() {
    lastPing = Date.now();
    try {
      const res = await nativeFetch('/Account/KeepAlive', { method: 'POST', headers: { 'RequestVerificationToken': token() } });
      if (res.status === 401) goToLogin();
    } catch { /* offline: the timer still runs */ }
  }

  function activity() {
    if (ended) return;
    lastActivity = Date.now();
    write(lastActivity);
    if (warned) return;   // while the warning is shown only its buttons count
    if (Date.now() - lastPing >= PING_EVERY) keepAlive();
  }
  let moveThrottle = 0;
  ['click', 'keydown', 'scroll', 'touchstart'].forEach(ev => document.addEventListener(ev, activity, { passive: true }));
  document.addEventListener('mousemove', () => { if (Date.now() - moveThrottle > 5000) { moveThrottle = Date.now(); activity(); } }, { passive: true });

  const modalId = 'sessionWarningModal';
  const countdown = document.getElementById('sessionCountdown');
  document.getElementById('sessionStayBtn')?.addEventListener('click', () => {
    warned = false;
    closeModal(modalId);
    lastActivity = Date.now(); write(lastActivity);
    keepAlive();
  });
  document.getElementById('sessionSignOutBtn')?.addEventListener('click', () => document.getElementById('sessionLogoutForm').submit());

  setInterval(() => {
    if (ended) return;
    const left = idle - (Date.now() - latest());
    if (left <= 0) {
      // end it on the server too, then land on the login page with "session expired"
      ended = true;
      closeModal(modalId);
      const form = document.getElementById('sessionLogoutForm');
      form.action = '/Account/Logout?reason=expired';
      form.submit();
      return;
    }
    if (left <= warn) {
      if (!warned) { warned = true; openModal(modalId); }
      const s = Math.ceil(left / 1000);
      if (countdown) countdown.textContent = `${Math.floor(s / 60)}:${String(s % 60).padStart(2, '0')}`;
    } else if (warned) {
      // activity in another tab
      warned = false;
      closeModal(modalId);
    }
  }, 1000);
}

/* ── Init All ────────────────────────────────────────────────── */
document.addEventListener('DOMContentLoaded', () => {
  initUrlSearch();
  initSidebar();
  initDarkMode();
  initModals();
  initTabs();
  initForms();
  initTableSearch();
  setActiveNav();
  initExport();
  initSession();
});
