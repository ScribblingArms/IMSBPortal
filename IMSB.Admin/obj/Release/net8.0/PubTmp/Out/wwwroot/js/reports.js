// Reports & Analytics: KPIs and charts for the selected month / category, the stall occupancy and artisan
// performance reports, the report library, and CSV / PDF exports (/Reports/Export/{id}?format=csv|pdf).
// Localized strings come from window.ReportsI18n (Views/Reports/Index.cshtml).
(function ReportsModule(){
  const I18N = window.ReportsI18n || {};
  function t(key, ...args){
    let s = I18N[key] || key;
    args.forEach((a, i) => { s = s.split('{' + i + '}').join(a); });
    return s;
  }
  function esc(v){
    return String(v == null ? '' : v).replace(/[&<>"']/g, c => ({ '&':'&amp;', '<':'&lt;', '>':'&gt;', '"':'&quot;', "'":'&#39;' }[c]));
  }
  const $ = id => document.getElementById(id);
  const lang = () => document.documentElement.lang || undefined;
  const money = v => '₹' + Number(v || 0).toLocaleString('en-IN', { maximumFractionDigits: 0 });
  const shortMoney = v => v >= 100000 ? '₹' + (v / 100000).toFixed(1) + 'L' : v >= 1000 ? '₹' + (v / 1000).toFixed(0) + 'K' : '₹' + v;
  const monthShort = ym => { const m = /^(\d{4})-(\d{2})$/.exec(ym || ''); return m ? new Date(+m[1], +m[2] - 1, 1).toLocaleDateString(lang(), { month: 'short', year: '2-digit' }) : ym; };
  const qs = obj => Object.entries(obj).filter(([, v]) => v !== '' && v != null).map(([k, v]) => `${k}=${encodeURIComponent(v)}`).join('&');
  const toast = (title, msg, type) => { if (typeof showToast === 'function') showToast(esc(title), esc(msg), type); };
  const debounce = (fn, ms) => { let h; return (...a) => { clearTimeout(h); h = setTimeout(() => fn(...a), ms); }; };

  // the app's chart colors: primary blue for the measured series, a muted tone for the comparison / remainder
  const C = { primary: '#2558a8', muted: '#94a3b8', light: '#c7d6ee' };
  const charts = {};
  let loadSeq = 0;

  const page = () => ({ month: $('reportMonth').value, categoryId: $('reportCategory').value });
  const stallFilters = () => ({ q: $('stallReportSearch').value.trim(), status: $('stallReportStatus').value, block: $('stallReportBlock').value });
  const artisanFilters = () => ({ q: $('artisanReportSearch').value.trim(), status: $('artisanReportStatus').value });

  function exportUrl(id, format){
    const extra = id === 'stalls' ? stallFilters() : id === 'artisans' ? artisanFilters() : {};
    return `/Reports/Export/${encodeURIComponent(id)}?` + qs(Object.assign({ format }, page(), extra));
  }
  function runExport(id, format){
    const url = exportUrl(id, format);
    // the PDF is a print-ready page that opens the print dialog (Save as PDF)
    if (format === 'pdf') window.open(url, '_blank', 'noopener');
    else window.location.href = url;
  }

  // ── KPIs + charts ─────────────────────────────────────────────
  async function loadOverview(){
    const seq = ++loadSeq;
    try {
      const res = await fetch('/Reports/Data?' + qs(page()));
      if (!res.ok){ console.warn('Reports: data request failed', res.status); return; }
      const d = await res.json();
      if (seq !== loadSeq) return;
      renderKpis(d.kpis, d.monthLabel);
      renderTrend(d.salesTrend);
      renderCategories(d.categoryRevenue);
      renderOccupancy(d.occupancyTrend);
    } catch (e) {
      console.error(e);
      toast(t('Error'), t('CouldNotReachServer'), 'error');
    }
  }

  function renderKpis(k, monthLabel){
    const set = (id, v) => { const el = $(id); if (el) el.textContent = v; };
    set('kpiTitle', t('KpiTitleFor', monthLabel));
    set('kpiSales', money(k.totalSales));
    set('kpiSalesChange', k.salesChangePct == null ? t('NoComparisonMonth')
      : t('VsLastMonth', (k.salesChangePct >= 0 ? '+' : '') + Number(k.salesChangePct).toFixed(1) + '%'));
    set('kpiOccupancy', `${k.occupiedStalls}/${k.totalStalls}`);
    set('kpiOccupancyPct', k.occupancyPct + '%');
    set('kpiArtisans', k.activeArtisans);
    set('kpiAttendance', k.avgAttendancePct == null ? '—' : Number(k.avgAttendancePct).toFixed(1) + '%');
    set('kpiApproved', k.applicationsApproved);
    set('kpiLowStock', k.lowStockItems);
  }

  function draw(id, config){
    if (typeof Chart === 'undefined') return;
    const canvas = $(id);
    if (!canvas) return;
    if (charts[id]) charts[id].destroy();
    charts[id] = new Chart(canvas, config);
  }
  function baseOptions(extra){
    const grid = getComputedStyle(document.body).getPropertyValue('--border').trim() || '#d6dce8';
    return Object.assign({
      responsive: true, maintainAspectRatio: false,
      interaction: { mode: 'index', intersect: false },
      scales: {
        x: { grid: { display: false } },
        y: { beginAtZero: true, grid: { color: grid }, ticks: { callback: shortMoney } }
      }
    }, extra || {});
  }

  function renderTrend(s){
    draw('monthlyTrendChart', {
      type: 'line',
      data: {
        labels: s.labels.map(monthShort),
        datasets: [
          { label: t('SalesThisYear'), data: s.current, borderColor: C.primary, backgroundColor: 'rgba(37,88,168,.08)', fill: true,
            tension: .35, borderWidth: 2, pointRadius: 4, pointHoverRadius: 6 },
          { label: t('SalesPreviousYear'), data: s.previous, borderColor: C.muted, borderDash: [5, 4], fill: false,
            tension: .35, borderWidth: 2, pointRadius: 0, pointHoverRadius: 5 }
        ]
      },
      options: baseOptions({ plugins: { legend: { position: 'bottom', labels: { boxWidth: 12 } },
        tooltip: { callbacks: { label: c => `${c.dataset.label}: ${money(c.parsed.y)}` } } } })
    });
  }

  function renderCategories(s){
    draw('revCatChart', {
      type: 'bar',
      data: { labels: s.labels, datasets: [{ label: t('Revenue'), data: s.values, backgroundColor: C.primary, borderRadius: 4, borderSkipped: 'start', maxBarThickness: 36 }] },
      options: baseOptions({ plugins: { legend: { display: false }, tooltip: { callbacks: { label: c => money(c.parsed.y) } } } })
    });
  }

  function renderOccupancy(s){
    const opts = baseOptions({ plugins: { legend: { position: 'bottom', labels: { boxWidth: 12 } },
      tooltip: { callbacks: { label: c => `${c.dataset.label}: ${c.parsed.y}`, footer: items => t('OfTotal', items.reduce((a, i) => a + i.parsed.y, 0)) } } } });
    opts.scales.x.stacked = true;
    opts.scales.y.stacked = true;
    opts.scales.y.ticks = { precision: 0 };
    draw('occupancyTrendChart', {
      type: 'bar',
      data: {
        labels: s.labels.map(monthShort),
        datasets: [
          { label: t('Occupied'), data: s.occupied, backgroundColor: C.primary, borderColor: getSurface(), borderWidth: { top: 2 }, maxBarThickness: 32 },
          { label: t('Vacant'), data: s.vacant, backgroundColor: C.light, borderRadius: { topLeft: 4, topRight: 4 }, borderSkipped: false, maxBarThickness: 32 }
        ]
      },
      options: opts
    });
  }
  function getSurface(){ return getComputedStyle(document.body).getPropertyValue('--surface').trim() || '#ffffff'; }

  // ── Stall occupancy report ────────────────────────────────────
  const STALL_STATUS = { OCCUPIED: ['Occupied', 'badge-success'], VACANT: ['Vacant', 'badge-neutral'], RESERVED: ['Reserved', 'badge-info'], MAINTENANCE: ['UnderMaintenance', 'badge-warning'] };
  const ARTISAN_STATUS = { ACTIVE: ['Active', 'badge-success'], ON_LEAVE: ['OnLeave', 'badge-warning'], RENEWAL_DUE: ['RenewalPending', 'badge-info'], INACTIVE: ['Inactive', 'badge-neutral'] };
  const badge = (map, s) => { const [k, cls] = map[s] || [s, 'badge-neutral']; return `<span class="badge ${cls}">${esc(t(k))}</span>`; };
  let blocksReady = false;
  let stallSeq = 0, artisanSeq = 0;

  async function loadStalls(){
    const seq = ++stallSeq;
    try {
      const res = await fetch('/Reports/StallReport?' + qs(Object.assign({}, page(), stallFilters())));
      if (!res.ok) return;
      const d = await res.json();
      if (seq !== stallSeq) return;
      if (!blocksReady){
        $('stallReportBlock').insertAdjacentHTML('beforeend', (d.blocks || []).map(b => `<option value="${esc(b)}">${esc(b)}</option>`).join(''));
        blocksReady = true;
      }
      $('stallReportCount').textContent = d.rows.length;
      $('stallReportBody').innerHTML = d.rows.length ? d.rows.map(r => `<tr>
        <td><strong>${esc(r.stallCode)}</strong></td>
        <td>${r.artisanName ? esc(r.artisanName) : `<span style="color:var(--text-muted)">${esc(t('NoStallAssigned'))}</span>`}</td>
        <td>${esc(r.category || '—')}</td>
        <td><strong style="color:var(--success)">${money(r.monthlySales)}</strong></td>
        <td>${r.products}</td><td>${r.stockUnits}</td>
        <td>${esc(t('DaysOfMonth', r.occupancyDays, r.daysInPeriod))}</td>
        <td>${badge(STALL_STATUS, r.status)}</td></tr>`).join('')
        : `<tr><td colspan="8" class="empty-state">${esc(t('NoStallsFound'))}</td></tr>`;
    } catch (e) { console.error(e); }
  }

  // ── Artisan performance report ────────────────────────────────
  async function loadArtisans(){
    const seq = ++artisanSeq;
    try {
      const res = await fetch('/Reports/ArtisanReport?' + qs(Object.assign({}, page(), artisanFilters())));
      if (!res.ok) return;
      const d = await res.json();
      if (seq !== artisanSeq) return;
      $('artisanReportCount').textContent = d.rows.length;
      $('artisanReportBody').innerHTML = d.rows.length ? d.rows.map(r => `<tr>
        <td><strong>${esc(r.name)}</strong><div style="font-size:.68rem;color:var(--text-muted)">${esc(r.artisanCode)} · ${esc(r.district)}</div></td>
        <td>${esc(r.category || '—')}</td>
        <td>${r.stallCode ? `<span class="badge badge-primary">${esc(r.stallCode)}</span>` : '—'}</td>
        <td><strong>${money(r.monthlySales)}</strong></td>
        <td>${r.unitsSold}</td><td>${r.products}</td>
        <td>${Number(r.sharePct).toFixed(1)}%</td>
        <td>${r.rating ? `<span class="rating" title="${esc(t('RatingOutOf', r.rating))}" aria-label="${esc(t('RatingOutOf', r.rating))}">${'★'.repeat(r.rating)}${'☆'.repeat(5 - r.rating)}</span>` : '—'}</td>
        <td>${badge(ARTISAN_STATUS, r.status)}</td></tr>`).join('')
        : `<tr><td colspan="9" class="empty-state">${esc(t('NoArtisansFound'))}</td></tr>`;
    } catch (e) { console.error(e); }
  }

  // ── Report library ────────────────────────────────────────────
  const LIBRARY = [
    { id: 'stalls', icon: 'fa-store', color: '#2558a8', bg: 'var(--primary-xlight)', title: 'RptStallOccupancy', desc: 'RptStallOccupancyDesc' },
    { id: 'inventory', icon: 'fa-boxes-stacked', color: '#e85d04', bg: 'var(--accent-light)', title: 'RptInventory', desc: 'RptInventoryDesc' },
    { id: 'artisans', icon: 'fa-palette', color: '#6366f1', bg: '#eef2ff', title: 'RptArtisan', desc: 'RptArtisanDesc' },
    { id: 'sales', icon: 'fa-chart-line', color: '#0369a1', bg: 'var(--info-light)', title: 'RptSales', desc: 'RptSalesDesc' },
    { id: 'applications', icon: 'fa-file-circle-check', color: '#b45309', bg: 'var(--warning-light)', title: 'RptApplications', desc: 'RptApplicationsDesc' },
    // attendance is not recorded in the system yet
    { id: null, icon: 'fa-users', color: '#1b7a3e', bg: 'var(--success-light)', title: 'RptAttendance', desc: 'RptAttendanceDesc' }
  ];

  function renderLibrary(){
    $('reportCards').innerHTML = LIBRARY.map(r => `
      <div class="report-card">
        <div class="report-card-icon" style="background:${r.bg};color:${r.color}"><i class="fa-solid ${r.icon}"></i></div>
        <div class="report-card-title">${esc(t(r.title))}</div>
        <div class="report-card-desc">${esc(t(r.desc))}</div>
        ${r.id ? `<div class="report-card-footer">
          <button type="button" class="btn btn-primary btn-sm" style="flex:1" data-report="${r.id}" data-format="pdf"><i class="fa-solid fa-file-pdf"></i> ${esc(t('ExportPdf'))}</button>
          <button type="button" class="btn btn-outline btn-sm" style="flex:1" data-report="${r.id}" data-format="csv"><i class="fa-solid fa-file-csv"></i> ${esc(t('ExportCsv'))}</button>
        </div>` : `<div class="report-card-footer"><span class="badge badge-neutral">${esc(t('ModuleNotConnected'))}</span></div>`}
      </div>`).join('');
  }

  // ── Wiring ────────────────────────────────────────────────────
  function loadAll(){ loadOverview(); loadStalls(); loadArtisans(); }

  document.addEventListener('DOMContentLoaded', () => {
    if (typeof Chart !== 'undefined') Chart.defaults.font.family = "'Poppins', sans-serif";
    renderLibrary();
    // every export button on the page, including the library cards
    document.querySelector('.page-body').addEventListener('click', e => {
      const btn = e.target.closest('[data-report]');
      if (btn) runExport(btn.dataset.report, btn.dataset.format || 'csv');
    });
    ['reportMonth', 'reportCategory'].forEach(id => $(id).addEventListener('change', loadAll));

    $('stallReportSearch').addEventListener('input', debounce(loadStalls, 250));
    ['stallReportStatus', 'stallReportBlock'].forEach(id => $(id).addEventListener('change', loadStalls));
    $('stallReportReset').addEventListener('click', () => { ['stallReportSearch', 'stallReportStatus', 'stallReportBlock'].forEach(id => { $(id).value = ''; }); loadStalls(); });

    $('artisanReportSearch').addEventListener('input', debounce(loadArtisans, 250));
    $('artisanReportStatus').addEventListener('change', loadArtisans);
    $('artisanReportReset').addEventListener('click', () => { ['artisanReportSearch', 'artisanReportStatus'].forEach(id => { $(id).value = ''; }); loadArtisans(); });

    loadAll();
  });
})();
