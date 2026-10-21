/* ============================================================
   IMSB Admin Portal – dashboard.js
   Dashboard cards, charts and lists from /Dashboard/Data.
   Localized strings come from window.DashboardI18n (Views/Dashboard/Index.cshtml).
   ============================================================ */
(function DashboardModule(){
  const charts = {};
  let loadSeq = 0;

  // ── i18n / formatting ─────────────────────────────────────────
  const I18N = window.DashboardI18n || {};
  function t(key, ...args){
    let s = I18N[key] || key;
    args.forEach((a, i) => { s = s.split('{' + i + '}').join(a); });
    return s;
  }
  function esc(v){
    return String(v == null ? '' : v).replace(/[&<>"']/g, c => ({ '&':'&amp;', '<':'&lt;', '>':'&gt;', '"':'&quot;', "'":'&#39;' }[c]));
  }
  const lang = () => document.documentElement.lang || undefined;
  const $ = id => document.getElementById(id);
  const money = v => '₹' + Number(v || 0).toLocaleString('en-IN', { maximumFractionDigits: 2 });
  const shortMoney = v => v >= 100000 ? '₹' + (v / 100000).toFixed(1) + 'L' : v >= 1000 ? '₹' + (v / 1000).toFixed(0) + 'K' : '₹' + v;
  const setEl = (id, v) => { const el = $(id); if (el) el.textContent = v; };
  function shortDate(iso){
    const m = /^(\d{4})-(\d{2})-(\d{2})$/.exec(iso || '');
    if (!m) return iso;
    return new Date(+m[1], +m[2] - 1, +m[3]).toLocaleDateString(lang(), { day: 'numeric', month: 'short' });
  }
  function monthName(ym){
    const m = /^(\d{4})-(\d{2})$/.exec(ym || '');
    return m ? new Date(+m[1], +m[2] - 1, 1).toLocaleDateString(lang(), { month: 'long', year: 'numeric' }) : ym;
  }
  function relativeTime(iso){
    const diff = (new Date(iso).getTime() - Date.now()) / 1000;   // negative = past
    if (Math.abs(diff) < 60) return t('JustNow');
    const rtf = new Intl.RelativeTimeFormat(lang(), { numeric: 'auto' });
    const units = [['day', 86400], ['hour', 3600], ['minute', 60]];
    for (const [unit, secs] of units) if (Math.abs(diff) >= secs) return rtf.format(Math.round(diff / secs), unit);
    return t('JustNow');
  }

  function chartDefaults(){
    if (typeof Chart === 'undefined') return;
    const isDark = document.body.classList.contains('dark-mode');
    Chart.defaults.font.family = "'Poppins', sans-serif";
    Chart.defaults.font.size = 11;
    Chart.defaults.color = isDark ? '#94a3b8' : '#64748b';
    Chart.defaults.plugins.legend.labels.boxWidth = 10;
    Chart.defaults.plugins.legend.labels.padding = 12;
  }
  function draw(id, config){
    const ctx = $(id);
    if (!ctx || typeof Chart === 'undefined') return;
    if (charts[id]) charts[id].destroy();
    charts[id] = new Chart(ctx, config);
  }

  // ── Stats ─────────────────────────────────────────────────────
  function renderStats(s){
    setEl('stat-stalls', s.totalStalls);
    setEl('stat-occupied', s.occupiedStalls);
    setEl('stat-vacant', s.vacantStalls);
    setEl('stat-occ-pct', t('OccupiedPct', s.occupancyPct));
    setEl('stat-artisans', s.totalArtisans == null ? '—' : s.totalArtisans);
    setEl('stat-sales', money(s.todaySales));
    const change = $('stat-sales-change');
    if (change){
      const pct = s.salesChangePct;
      change.textContent = pct == null ? t('NoComparison') : t('VsYesterday', (pct >= 0 ? '+' : '') + Number(pct).toFixed(1) + '%');
      change.classList.toggle('down', pct != null && pct < 0);
      change.classList.toggle('up', !(pct != null && pct < 0));
    }
    setEl('stat-lowstock', s.lowStockItems);
    setEl('stat-pending', s.pendingApplications == null ? '—' : s.pendingApplications);
    const banner = $('lowStockBanner');
    if (banner) banner.style.display = s.lowStockItems ? '' : 'none';
  }

  function renderApplications(apps){
    const note = $('applicationsNote');
    if (!apps){ if (note) note.style.display = ''; return; }
    if (note) note.style.display = 'none';
    const total = (apps.approved + apps.review + apps.pending) || 1;
    [['approved', apps.approved], ['review', apps.review], ['pending', apps.pending]].forEach(([k, v]) => {
      setEl('aw-' + k, v);
      const bar = $('aw-' + k + '-bar'); if (bar) bar.style.width = Math.round(v / total * 100) + '%';
    });
  }

  // ── Charts ────────────────────────────────────────────────────
  function renderSalesTrend(trend, period){
    setEl('salesTrendSubtitle', t('DailySalesOverDays', period));
    draw('salesTrendChart', {
      type: 'line',
      data: {
        labels: trend.labels.map(shortDate),
        datasets: [{ label: t('DailySales'), data: trend.values, borderColor: '#2558a8', backgroundColor: 'rgba(37,88,168,.1)',
          borderWidth: 2.5, pointBackgroundColor: '#2558a8', pointRadius: period > 60 ? 0 : 3, pointHoverRadius: 5, fill: true, tension: .35 }]
      },
      options: {
        responsive: true, maintainAspectRatio: false,
        plugins: { legend: { display: false }, tooltip: { callbacks: { label: c => money(c.parsed.y) } } },
        scales: { x: { grid: { display: false }, ticks: { maxTicksLimit: 10 } }, y: { beginAtZero: true, ticks: { callback: shortMoney } } }
      }
    });
  }

  function renderOccupancy(o){
    draw('stallOccupancyChart', {
      type: 'doughnut',
      data: { labels: [t('Occupied'), t('Vacant')], datasets: [{ data: [o.occupied, o.vacant], backgroundColor: ['#2558a8', '#e8f0fb'], borderWidth: 3 }] },
      options: {
        responsive: true, maintainAspectRatio: false, cutout: '70%',
        plugins: { legend: { position: 'bottom' }, tooltip: { callbacks: { label: c => `${c.label}: ${c.parsed}` } } }
      }
    });
  }

  function renderCategory(c){
    setEl('categorySubtitle', t('CategoryMonthSubtitle', monthName(c.month)));
    const colors = ['#2558a8','#e85d04','#1b7a3e','#6366f1','#0369a1','#b45309','#be123c','#0891b2','#7c3aed','#059669'];
    draw('categoryChart', {
      type: 'bar',
      data: { labels: c.labels, datasets: [{ label: t('SalesAmount'), data: c.values, backgroundColor: c.labels.map((_, i) => colors[i % colors.length]), borderRadius: 5, borderSkipped: false }] },
      options: {
        responsive: true, maintainAspectRatio: false,
        plugins: { legend: { display: false }, tooltip: { callbacks: { label: x => money(x.parsed.y) } } },
        scales: { x: { grid: { display: false } }, y: { beginAtZero: true, ticks: { callback: shortMoney } } }
      }
    });
  }

  // attendance has no data source yet: show an empty state instead of sample figures
  function renderAttendance(att, dept){
    const empty = (canvasId, emptyId, show) => {
      const c = $(canvasId), e = $(emptyId);
      if (c) c.style.display = show ? 'none' : '';
      if (e){ e.style.display = show ? '' : 'none'; e.textContent = t('AttendanceNotConnected'); }
    };
    empty('attendanceChart', 'attendanceEmpty', !att);
    empty('deptAttChart', 'deptAttEmpty', !dept);
    if (att){
      draw('attendanceChart', {
        type: 'bar',
        data: { labels: att.labels, datasets: [
          { label: t('Present'), data: att.present, backgroundColor: 'rgba(27,122,62,.2)', borderColor: '#1b7a3e', borderWidth: 2, borderRadius: 4 },
          { label: t('AttendancePercent'), data: att.percent, type: 'line', borderColor: '#e85d04', backgroundColor: 'transparent', borderWidth: 2, pointRadius: 4, yAxisID: 'pct', tension: .4 }
        ] },
        options: {
          responsive: true, maintainAspectRatio: false, plugins: { legend: { position: 'top' } },
          scales: {
            x: { grid: { display: false } },
            y: { title: { display: true, text: t('StaffCount') }, beginAtZero: true },
            pct: { position: 'right', min: 0, max: 100, grid: { drawOnChartArea: false }, ticks: { callback: v => v + '%' } }
          }
        }
      });
    }
    if (dept){
      draw('deptAttChart', {
        type: 'doughnut',
        data: { labels: dept.labels, datasets: [{ data: dept.values, backgroundColor: ['#2558a8','#e85d04','#1b7a3e','#6366f1','#0369a1','#b45309'], borderWidth: 2 }] },
        options: { responsive: true, maintainAspectRatio: false, cutout: '65%',
          plugins: { legend: { position: 'right', labels: { font: { size: 10 } } }, tooltip: { callbacks: { label: c => `${c.label}: ${c.parsed}%` } } } }
      });
    }
  }

  // ── Lists ─────────────────────────────────────────────────────
  function renderPerformers(top, low){
    const ranks = ['gold', 'silver', 'bronze'];
    const item = (s, i, color, rank) => `
      <div class="performer-item">
        <div class="performer-rank ${rank || ''}">${i + 1}</div>
        <div class="performer-info">
          <div class="performer-name">${esc(s.stallCode)}</div>
          <div class="performer-cat">${esc(s.category || '—')} · ${esc(t('UnitsSold', s.quantity))}</div>
        </div>
        <div class="performer-bar-wrap"><div class="performer-bar"><div class="performer-bar-fill" style="width:${s.pct}%;background:${color}"></div></div></div>
        <div class="performer-value"${color === 'var(--danger)' ? ' style="color:var(--danger)"' : ''}>${money(s.revenue)}</div>
      </div>`;
    const topEl = $('topPerformers'), lowEl = $('lowPerformers');
    if (topEl) topEl.innerHTML = top.length ? top.map((s, i) => item(s, i, 'var(--primary-light)', ranks[i])).join('') : `<div class="empty-state">${esc(t('NoSalesYet'))}</div>`;
    if (lowEl) lowEl.innerHTML = low.length ? low.map((s, i) => item(s, i, 'var(--danger)')).join('') : `<div class="empty-state">${esc(t('NoStallsYet'))}</div>`;
  }

  function renderLowStock(list){
    const el = $('lowStockList'); if (!el) return;
    if (!list.length){ el.innerHTML = `<div class="empty-state">${esc(t('NoLowStock'))}</div>`; return; }
    el.innerHTML = list.map(s => `
      <div class="alert-list-item stock-low">
        <div class="alert-list-icon"><i class="fa-solid fa-box-open"></i></div>
        <div class="alert-list-info">
          <div class="alert-list-name">${esc(s.name)} <span class="badge ${s.urgency === 'CRITICAL' ? 'badge-danger' : 'badge-warning'}" style="font-size:.6rem">${esc(t(s.urgency === 'CRITICAL' ? 'Critical' : 'Moderate'))}</span></div>
          <div class="alert-list-cat">${esc(s.categoryName)} · ${esc(s.stallCode)}</div>
          <div class="alert-list-qty">${esc(t('OnlyLeft', s.quantity, s.minStock))}</div>
        </div>
        <div class="stock-level"><div class="stock-level-fill" style="width:${s.pct}%"></div></div>
      </div>`).join('');
  }

  function activityView(a){
    switch (a.type){
      case 'SALE': return { icon: 'fa-cash-register', color: 'green', title: t('ActSale'), desc: t('ActSaleDesc', a.subject, money(a.amount), a.quantity) };
      case 'STOCK_ADD': return { icon: 'fa-box', color: 'blue', title: t('ActStockAdd'), desc: t('ActStockDesc', a.subject, a.quantity) };
      case 'STOCK_REDUCE': return { icon: 'fa-box', color: 'orange', title: t('ActStockReduce'), desc: t('ActStockDesc', a.subject, a.quantity) };
      case 'STOCK_CORRECTION': return { icon: 'fa-scale-balanced', color: 'orange', title: t('ActStockCorrection'), desc: t('ActStockDesc', a.subject, a.quantity) };
      case 'STOCK_INITIAL': return { icon: 'fa-plus', color: 'blue', title: t('ActStockInitial'), desc: t('ActStockDesc', a.subject, a.quantity) };
      case 'STAFF_ADDED': return { icon: 'fa-user-plus', color: 'blue', title: t('ActStaffAdded'), desc: t('ActStaffDesc', a.subject, a.reference) };
      default: return { icon: 'fa-circle', color: 'blue', title: a.type, desc: a.subject };
    }
  }
  function renderActivity(list){
    const el = $('activityFeed'); if (!el) return;
    if (!list.length){ el.innerHTML = `<div class="empty-state">${esc(t('NoActivity'))}</div>`; return; }
    el.innerHTML = list.map(a => {
      const v = activityView(a);
      return `<div class="activity-item">
        <div class="activity-dot ${v.color}"><i class="fa-solid ${v.icon}"></i></div>
        <div class="activity-text">
          <div class="activity-title">${esc(v.title)}</div>
          <div class="activity-desc">${esc(v.desc)}</div>
          <div class="activity-time"><i class="fa-regular fa-clock"></i> ${esc(relativeTime(a.at))}${a.by ? ' · ' + esc(a.by) : ''}</div>
        </div>
      </div>`;
    }).join('');
  }

  // ── Load ──────────────────────────────────────────────────────
  const period = () => $('trendPeriod')?.value || '40';

  async function load(){
    const seq = ++loadSeq;
    try {
      const res = await fetch('/Dashboard/Data?days=' + encodeURIComponent(period()));
      if (!res.ok){ console.warn('Dashboard: data request failed', res.status); return; }
      const d = await res.json();
      if (seq !== loadSeq) return;
      renderStats(d.stats);
      renderApplications(d.applications);
      renderPerformers(d.topStalls, d.lowStalls);
      renderLowStock(d.lowStock);
      renderActivity(d.activity);
      chartDefaults();
      renderSalesTrend(d.salesTrend, d.period);
      renderOccupancy(d.occupancy);
      renderCategory(d.categorySales);
      renderAttendance(d.attendance, d.deptAttendance);
    } catch (e) {
      console.error(e);
      if (typeof showToast === 'function') showToast(t('Error'), t('CouldNotReachServer'), 'error');
    }
  }

  document.addEventListener('DOMContentLoaded', () => {
    setEl('dashDate', new Date().toLocaleDateString(lang(), { weekday: 'long', year: 'numeric', month: 'long', day: 'numeric' }));
    $('trendPeriod')?.addEventListener('change', load);
    $('dashboardExportBtn')?.addEventListener('click', () => {
      window.location.href = '/Dashboard/Export?days=' + encodeURIComponent(period());
    });
    load();
  });
})();
