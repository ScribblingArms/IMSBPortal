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

/* ── Mobile / email / Aadhaar ────────────────────────────────── */
// The browser copy of the server rules (MobileNumbers, EmailAddresses and Aadhaar in IMSB.Dto), used by every form.
const IMSBValidate = {
  mobile: v => /^[6-9][0-9]{9}$/.test(v || ''),
  email: v => new RegExp("^[A-Za-z0-9_%+-]+([.][A-Za-z0-9_%+-]+)*@[A-Za-z0-9]+(-+[A-Za-z0-9]+)*([.][A-Za-z0-9]+(-+[A-Za-z0-9]+)*)*[.][A-Za-z]{2,}$").test(v || ''),
  aadhaar: v => /^[0-9]{12}$/.test((v || '').replace(/[-\s]/g, ''))
};

// Inputs with data-format="mobile" or "aadhaar" keep to their format while typing or pasting.
(function initFormattedInputs() {
  const formats = {
    mobile(value) {
      let digits = value.replace(/\D/g, '');
      // a pasted "+91 98765 43210" or "098765 43210" is the 10-digit number
      if (digits.length === 12 && digits.startsWith('91')) digits = digits.slice(2);
      else if (digits.length === 11 && digits.startsWith('0')) digits = digits.slice(1);
      return digits.slice(0, 10);
    },
    aadhaar: value => (value.replace(/\D/g, '').slice(0, 12).match(/\d{1,4}/g) || []).join('-')
  };

  document.addEventListener('input', e => {
    const el = e.target;
    const format = el.dataset && formats[el.dataset.format];
    if (!format) return;
    const formatted = format(el.value);
    if (formatted === el.value) return;
    // keep the caret after the same digit, so correcting the middle of a number does not jump to the end
    const digitsBefore = el.value.slice(0, el.selectionStart).replace(/\D/g, '').length;
    el.value = formatted;
    let caret = 0;
    for (let seen = 0; caret < formatted.length && seen < digitsBefore; caret++) if (/\d/.test(formatted[caret])) seen++;
    el.setSelectionRange(caret, caret);
  });
})();

/* ── Date fields ─────────────────────────────────────────────── */
// Browsers show "dd-mm-yyyy" in an empty date field and CSS cannot detect that, so empty fields get .date-empty,
// which forms.css uses to hide the text. It comes back as soon as a key is pressed in the field, and half-typed dates stay visible (badInput).
(function initDateFields() {
  const DATE_TYPES = ['date', 'datetime-local', 'month', 'week'];
  const isDateField = el => el instanceof HTMLInputElement && DATE_TYPES.includes(el.type);
  const sync = el => {
    if (isDateField(el)) el.classList.toggle('date-empty', el.value === '' && !el.validity.badInput);
  };
  const syncAll = root => root.querySelectorAll('input').forEach(sync);

  // pages fill their edit forms by assigning .value, which fires no event
  const value = Object.getOwnPropertyDescriptor(HTMLInputElement.prototype, 'value');
  Object.defineProperty(HTMLInputElement.prototype, 'value', {
    ...value,
    set(next) {
      value.set.call(this, next);
      sync(this);
    }
  });

  ['input', 'change', 'focusout'].forEach(type => document.addEventListener(type, e => sync(e.target), true));
  // typing must be seen from the first key; leaving the field untouched hides the text again (focusout above)
  document.addEventListener('keydown', e => {
    if (isDateField(e.target) && !['Tab', 'Shift', 'Escape', 'Enter', 'Alt', 'Control', 'Meta'].includes(e.key)) e.target.classList.remove('date-empty');
  }, true);
  // the fields are only cleared after the reset event has finished
  document.addEventListener('reset', e => setTimeout(() => syncAll(e.target)));
  syncAll(document);
})();

/* ── Sidebar Toggle ──────────────────────────────────────────── */
function initSidebar() {
  const sidebar = document.getElementById('sidebar');
  const toggleBtn = document.getElementById('sidebarToggle');
  if (!sidebar) return;

  if (toggleBtn) {
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

    document.addEventListener('click', e => {
      if (window.innerWidth <= 700 && !sidebar.contains(e.target) && e.target !== toggleBtn) {
        sidebar.classList.remove('mobile-open');
      }
    });
  }

  sidebar.querySelectorAll('[data-nav-group]').forEach(group => {
    const toggle = group.querySelector('.nav-group-toggle');
    if (!toggle) return;
    toggle.addEventListener('click', () => {
      const isOpen = group.classList.toggle('is-open');
      toggle.setAttribute('aria-expanded', String(isOpen));
    });
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

    const initialTab = window.location.hash.slice(1);
    const matchingTab = [...bar.querySelectorAll('.tab-item')].find(tab => tab.dataset.tab === initialTab);
    if (matchingTab) matchingTab.click();
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
  const normalize = path => {
    const segments = path.split('/').filter(Boolean);
    if (segments.length > 1 && segments[segments.length - 1].toLowerCase() === 'index') segments.pop();
    return '/' + segments.join('/').toLowerCase();
  };
  const currentPage = normalize(window.location.pathname);
  const currentTab = window.location.hash.slice(1);
  const items = [...document.querySelectorAll('.nav-item[data-page]')];
  const controller = currentPage.split('/').filter(Boolean)[0];
  const selected = items.find(item => normalize(item.dataset.page) === currentPage &&
      item.dataset.tabTarget && item.dataset.tabTarget === currentTab) ||
    items.find(item => normalize(item.dataset.page) === currentPage && !item.dataset.tabTarget) ||
    items.find(item => normalize(item.dataset.page).split('/').filter(Boolean)[0] === controller);
  items.forEach(item => {
    item.classList.toggle('active', item === selected);
  });

  document.querySelectorAll('[data-nav-group]').forEach(group => {
    const isActive = group.contains(selected);
    group.classList.toggle('is-open', isActive);
    const toggle = group.querySelector('.nav-group-toggle');
    if (toggle) toggle.setAttribute('aria-expanded', String(isActive));
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
