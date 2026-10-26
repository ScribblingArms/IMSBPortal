// Sales page: daily sales entry (create / edit / delete), product-wise report, charts and the auto stock deduction log
// through the /Sales endpoints. Localized strings come from window.SalesI18n (rendered by Views/Sales/Index.cshtml).
(function SalesModule(){
  let productOptions = [];  // all products (code, name, categoryName, stallCode, unitPrice, quantity)
  let entries = [];         // entries on the selected date
  let editing = null;       // entry being edited: { entryNo, items }
  let rowSeq = 0;
  let saving = false;
  let dirty = false;
  let loadSeq = 0, reportSeq = 0;
  let catChart = null, hourlyChart = null;

  // ── i18n / formatting ─────────────────────────────────────────
  const I18N = window.SalesI18n || {};
  function t(key, ...args){
    let s = I18N[key] || key;
    args.forEach((a, i) => { s = s.split('{' + i + '}').join(a); });
    return s;
  }
  function esc(v){
    return String(v == null ? '' : v).replace(/[&<>"']/g, c => ({ '&':'&amp;', '<':'&lt;', '>':'&gt;', '"':'&quot;', "'":'&#39;' }[c]));
  }
  const lang = () => document.documentElement.lang || undefined;
  const money = v => '₹' + Number(v || 0).toLocaleString('en-IN', { maximumFractionDigits: 2 });
  const money2 = v => '₹' + Number(v || 0).toLocaleString('en-IN', { minimumFractionDigits: 2, maximumFractionDigits: 2 });
  const shortMoney = v => v >= 100000 ? '₹' + (v / 100000).toFixed(1) + 'L' : v >= 1000 ? '₹' + (v / 1000).toFixed(0) + 'K' : '₹' + v;
  const signed = p => (p >= 0 ? '+' : '') + Number(p).toFixed(1) + '%';
  function formatDate(iso){
    const m = /^(\d{4})-(\d{2})-(\d{2})$/.exec(iso || '');
    if (!m) return iso || '—';
    const d = new Date(+m[1], +m[2] - 1, +m[3]);
    return `${m[3]} ${d.toLocaleString(lang(), { month: 'short' })} ${m[1]}`;
  }
  function isoOf(d){ return `${d.getFullYear()}-${String(d.getMonth()+1).padStart(2,'0')}-${String(d.getDate()).padStart(2,'0')}`; }
  const todayIso = () => isoOf(new Date());
  const nowTime = () => { const d = new Date(); return `${String(d.getHours()).padStart(2,'0')}:${String(d.getMinutes()).padStart(2,'0')}`; };
  const hourLabel = h => new Date(2000, 0, 1, h).toLocaleTimeString(lang(), { hour: 'numeric' });
  const token = () => document.querySelector('#salesEntryForm input[name="__RequestVerificationToken"]')?.value || '';
  const $ = id => document.getElementById(id);
  const toast = (title, msg, type) => { if (typeof showToast === 'function') showToast(title, msg, type); };
  const selectedDate = () => $('salesDate').value || todayIso();
  const selectedStall = () => $('salesStall').value;
  const statusBadge = s => s === 'IN_STOCK' ? 'badge-success' : s === 'LOW_STOCK' ? 'badge-warning' : 'badge-danger';
  const statusLabel = s => s === 'OUT_OF_STOCK' ? t('OutOfStock') : s === 'LOW_STOCK' ? t('LowStock') : t('InStock');
  const qs = obj => Object.entries(obj).filter(([, v]) => v !== '' && v != null).map(([k, v]) => `${k}=${encodeURIComponent(v)}`).join('&');

  // ── Load (daily data, charts) ─────────────────────────────────
  async function load(){
    const seq = ++loadSeq;
    try {
      const res = await fetch('/Sales/Data?' + qs({ date: selectedDate(), stall: selectedStall() }));
      if (!res.ok){ console.warn('Sales: data request failed', res.status); return; }
      const json = await res.json();
      if (seq !== loadSeq) return;
      productOptions = json.productOptions || [];
      entries = json.entries || [];
      renderSummary(json.summary, json.today);
      renderStallSelects(json.stalls || []);
      renderCategorySelect(json.categories || []);
      refreshProductSelects();
      renderEntries();
      renderCharts(json);
    } catch (e) {
      console.error(e);
      toast(t('Error'), t('CouldNotReachServer'), 'error');
    }
  }

  function renderSummary(s, today){
    const set = (id, v) => { const el = $(id); if (el) el.textContent = v; };
    const isToday = s.date === todayIso();
    set('todayTotal', money(s.totalSales));
    set('todayTotalLabel', isToday ? t('TodaysTotalSales') : t('TotalSalesForDay'));
    set('todayItems', s.itemsSold);
    set('todayTxns', s.transactions);
    set('todayStalls', s.activeStalls);
    set('todayDate', formatDate(s.date));

    const change = (id, pct, key) => {
      const el = $(id); if (!el) return;
      el.textContent = pct == null ? t('NoComparison') : t(key, signed(pct));
      el.classList.toggle('down', pct != null && pct < 0);
      el.classList.toggle('up', !(pct != null && pct < 0));
    };
    set('statDaySales', money(today.totalSales));
    change('statDayChange', today.changeVsPreviousDay, 'VsYesterday');
    set('statMonthSales', money(today.monthSales));
    change('statMonthChange', today.changeVsPreviousMonth, 'VsLastMonth');
    set('statTxns', today.transactions);
    set('statAvgTxn', t('AvgPerTxn', money(today.averageTransaction)));
    set('statActiveStalls', today.activeStalls);
    set('statStallsPct', t('PercentOfTotal', today.activeStallsPct));
  }

  function fillSelect(sel, values, allLabelOption){
    if (!sel) return;
    const current = sel.value;
    sel.innerHTML = allLabelOption + values.map(v => `<option value="${esc(v.value)}">${esc(v.label)}</option>`).join('');
    if ([...sel.options].some(o => o.value === current)) sel.value = current;
  }
  function renderStallSelects(stalls){
    const opts = stalls.map(s => ({ value: s, label: s }));
    ['salesStall', 'reportStall'].forEach(id => {
      const sel = $(id); if (!sel) return;
      fillSelect(sel, opts, sel.options[0]?.outerHTML || '');
    });
  }
  let categoriesRendered = false;
  function renderCategorySelect(categories){
    if (categoriesRendered) return;
    const sel = $('reportCategory');
    fillSelect(sel, categories.map(c => ({ value: c.id, label: c.name })), sel.options[0]?.outerHTML || '');
    categoriesRendered = true;
  }

  // ── Entry form rows ───────────────────────────────────────────
  function productLabel(p){ return `${p.name} (${p.stallCode})`; }
  function optionsFor(selectedCode){
    const stall = selectedStall();
    const list = productOptions.filter(p => !stall || p.stallCode.toUpperCase() === stall || p.code === selectedCode);
    return `<option value="">${esc(t('SelectProductOption'))}</option>` +
      list.map(p => `<option value="${esc(p.code)}"${p.code === selectedCode ? ' selected' : ''}>${esc(productLabel(p))}</option>`).join('');
  }
  // stock that can still be sold on a row: current stock + what the edited entry already took
  function availableFor(code){
    const p = productOptions.find(x => x.code === code);
    if (!p) return null;
    const already = editing ? editing.items.filter(i => i.code === code).reduce((a, i) => a + i.quantity, 0) : 0;
    return p.quantity + already;
  }

  function addRow(item){
    const container = $('salesEntryRows'); if (!container) return;
    const i = rowSeq++;
    const div = document.createElement('div');
    div.className = 'sales-entry-row';
    div.dataset.row = i;
    div.innerHTML = `
      <div class="form-group">
        <select class="form-control row-product" aria-label="${esc(t('SelectProductOption'))}">${optionsFor(item?.code)}</select>
        <div class="row-hint row-stock"></div>
        <div class="form-error row-err-product_code"></div>
      </div>
      <div class="form-group">
        <input type="number" class="form-control row-qty" min="1" step="1" value="${item ? item.quantity : 1}">
        <div class="form-error row-err-quantity"></div>
      </div>
      <div class="form-group">
        <input type="number" class="form-control row-price" min="0" step="0.01" value="${item ? item.unitPrice : ''}">
        <div class="form-error row-err-unit_price"></div>
      </div>
      <div class="row-total">₹0.00</div>
      <button type="button" class="btn btn-ghost btn-sm btn-icon row-remove" style="color:var(--danger)" aria-label="${esc(t('Delete'))}"><i class="fa-solid fa-trash"></i></button>`;
    container.appendChild(div);

    const sel = div.querySelector('.row-product');
    sel.addEventListener('change', () => {
      const p = productOptions.find(x => x.code === sel.value);
      if (p) div.querySelector('.row-price').value = p.unitPrice;   // product's price by default; editable (discounts)
      dirty = true; updateRow(div);
    });
    div.querySelectorAll('.row-qty, .row-price').forEach(el => el.addEventListener('input', () => { dirty = true; updateRow(div); }));
    div.querySelector('.row-remove').addEventListener('click', () => { div.remove(); dirty = true; updateTotals(); });
    updateRow(div);
  }

  function rowValues(div){
    return {
      productCode: div.querySelector('.row-product').value,
      quantity: Number(div.querySelector('.row-qty').value),
      unitPrice: Number(div.querySelector('.row-price').value)
    };
  }
  function updateRow(div){
    const v = rowValues(div);
    const total = (Number.isFinite(v.quantity) ? v.quantity : 0) * (Number.isFinite(v.unitPrice) ? v.unitPrice : 0);
    div.querySelector('.row-total').textContent = money2(total);
    const avail = availableFor(v.productCode);
    const hint = div.querySelector('.row-stock');
    hint.textContent = avail == null ? '' : t('StockAvailable', avail);
    hint.style.color = avail != null && v.quantity > avail ? 'var(--danger)' : '';
    updateTotals();
  }
  function updateTotals(){
    const total = [...document.querySelectorAll('#salesEntryRows .sales-entry-row')]
      .map(rowValues).reduce((a, v) => a + (Number.isFinite(v.quantity) && Number.isFinite(v.unitPrice) ? v.quantity * v.unitPrice : 0), 0);
    $('entryTotal').textContent = money2(total);
  }
  function refreshProductSelects(){
    document.querySelectorAll('#salesEntryRows .sales-entry-row').forEach(div => {
      const sel = div.querySelector('.row-product');
      sel.innerHTML = optionsFor(sel.value);
      updateRow(div);
    });
  }

  function resetForm(keepDate){
    $('salesEntryRows').innerHTML = '';
    $('salesRemarks').value = '';
    clearErrors();
    editing = null;
    dirty = false;
    $('entryFormTitle').textContent = $('entryFormTitle').dataset.title;
    $('saveSalesText').textContent = t('SaveSalesEntry');
    $('cancelEditBtn').style.display = 'none';
    if (!keepDate){ $('salesDate').value = todayIso(); }
    $('salesTime').value = $('salesDate').value === todayIso() ? nowTime() : '12:00';
    addRow();
    updateTotals();
  }

  // ── Validation / errors ───────────────────────────────────────
  function clearErrors(){
    const form = $('salesEntryForm');
    form.querySelectorAll('.form-group.has-error').forEach(g => g.classList.remove('has-error'));
    form.querySelectorAll('.form-error').forEach(e => { e.textContent = ''; e.style.display = ''; });
  }
  function showError(el, msg){
    if (!el) return null;
    el.textContent = msg;
    const group = el.closest('.form-group');
    if (group) group.classList.add('has-error'); else el.style.display = 'block';
    return group?.querySelector('input,select,textarea') || null;
  }
  // server keys: date, time, remarks, items, items_{i}_product_code|quantity|unit_price (i = index in the submitted list)
  function showErrors(errors, rows){
    let first = null;
    Object.entries(errors).forEach(([k, msg]) => {
      const m = /^items_(\d+)_(\w+)$/.exec(k);
      const el = m ? rows[+m[1]]?.querySelector('.row-err-' + m[2]) : $('err_' + k);
      const focus = showError(el, msg);
      if (!first) first = focus;
    });
    first?.focus();
  }
  function validate(body, rows){
    const e = {};
    if (!body.date) e.date = t('ValDate');
    else if (body.date > todayIso()) e.date = t('ValDateFuture');
    else if (body.date === todayIso() && body.time && body.time > nowTime()) e.time = t('ValTimeFuture');
    if (body.remarks && body.remarks.length > 500) e.remarks = t('ValRemarks');
    if (!body.items.length) e.items = t('ValSalesItemsRequired');
    const seen = new Set();
    body.items.forEach((it, i) => {
      if (!it.productCode) e[`items_${i}_product_code`] = t('ValSelectProduct');
      else if (seen.has(it.productCode)) e[`items_${i}_product_code`] = t('ValDuplicateProduct');
      seen.add(it.productCode);
      if (!Number.isInteger(it.quantity) || it.quantity < 1) e[`items_${i}_quantity`] = t('ValQuantityPositive');
      else {
        const avail = availableFor(it.productCode);
        if (avail != null && it.quantity > avail) e[`items_${i}_quantity`] = t('ValInsufficientStock', avail);
      }
      if (!Number.isFinite(it.unitPrice) || it.unitPrice < 0 || rows[i]?.querySelector('.row-price').value === '')
        e[`items_${i}_unit_price`] = t('ValUnitPrice');
    });
    return e;
  }

  async function save(){
    if (saving) return;
    clearErrors();
    // rows left completely empty are ignored, so a spare blank row does not block saving
    const rows = [...document.querySelectorAll('#salesEntryRows .sales-entry-row')]
      .filter(div => { const v = rowValues(div); return v.productCode || div.querySelector('.row-price').value !== ''; });
    const body = {
      date: $('salesDate').value,
      time: $('salesTime').value || null,
      remarks: $('salesRemarks').value.trim() || null,
      items: rows.map(rowValues)
    };
    if (!body.items.length){ toast(t('Error'), t('NothingToSubmit'), 'warning'); return; }
    const errors = validate(body, rows);
    if (Object.keys(errors).length){ showErrors(errors, rows); toast(t('Error'), t('CheckForm'), 'error'); return; }

    saving = true;
    $('saveSalesBtn').disabled = true;
    try {
      const url = editing ? '/Sales/Update/' + encodeURIComponent(editing.entryNo) : '/Sales/Create';
      const res = await fetch(url, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json', 'RequestVerificationToken': token() },
        body: JSON.stringify(body)
      });
      if (res.status === 400){
        const json = await res.json().catch(() => ({}));
        if (json.errors) showErrors(json.errors, rows);
        toast(t('Error'), t('CheckForm'), 'error');
        return;
      }
      if (res.status === 404){ toast(t('Error'), t('SaleNotFound'), 'error'); resetForm(true); await refreshAll(); return; }
      if (!res.ok){ toast(t('Error'), `${t('CheckForm')} (${res.status})`, 'error'); return; }
      const json = await res.json();
      toast(t('Success'), t(editing ? 'SalesUpdated' : 'SalesSaved', json.entryNo, money(json.total)), 'success');
      resetForm(true);
      await refreshAll();
    } catch (e) {
      console.error(e);
      toast(t('Error'), t('CouldNotReachServer'), 'error');
    } finally {
      saving = false;
      $('saveSalesBtn').disabled = false;
    }
  }

  // ── Entries list (view / edit / delete) ───────────────────────
  function renderEntries(){
    const body = $('salesEntriesBody');
    $('entriesCount').textContent = entries.length;
    if (!entries.length){ body.innerHTML = `<tr><td colspan="7" class="empty-state">${esc(t('NoSalesEntries'))}</td></tr>`; return; }
    body.innerHTML = entries.map(e => {
      const names = e.items.map(i => esc(i.name)).slice(0, 3).join(', ') + (e.items.length > 3 ? ` +${e.items.length - 3}` : '');
      return `<tr>
        <td><strong>${esc(e.entryNo)}</strong></td>
        <td>${esc(e.time)}</td>
        <td>${names} <span style="font-size:.68rem;color:var(--text-muted)">· ${esc(t('ProductsCount', e.items.length))}</span></td>
        <td>${e.quantity}</td>
        <td><strong style="color:var(--success)">${money(e.total)}</strong></td>
        <td>${esc(e.by || '—')}</td>
        <td style="white-space:nowrap">
          <button type="button" class="btn btn-ghost btn-sm btn-icon" data-act="view" data-id="${esc(e.entryNo)}" title="${esc(t('View'))}" aria-label="${esc(t('View'))}"><i class="fa-solid fa-eye"></i></button>
          <button type="button" class="btn btn-ghost btn-sm btn-icon" data-act="edit" data-id="${esc(e.entryNo)}" title="${esc(t('Edit'))}" aria-label="${esc(t('Edit'))}"><i class="fa-solid fa-pen"></i></button>
          <button type="button" class="btn btn-ghost btn-sm btn-icon" data-act="delete" data-id="${esc(e.entryNo)}" title="${esc(t('Delete'))}" aria-label="${esc(t('Delete'))}" style="color:var(--danger)"><i class="fa-solid fa-trash"></i></button>
        </td></tr>`;
    }).join('');
  }

  function viewEntry(entryNo){
    const e = entries.find(x => x.entryNo === entryNo);
    if (!e){ toast(t('Error'), t('SaleNotFound'), 'error'); return; }
    $('saleViewTitle').textContent = `${t('EntryNo')}: ${e.entryNo}`;
    $('saleViewBody').innerHTML = `
      <div style="font-size:.8rem;color:var(--text-muted);margin-bottom:10px">${esc(formatDate(e.date))} ${esc(e.time)} · ${esc(e.by || '—')}</div>
      <div class="table-container"><table class="data-table">
        <tbody>${e.items.map(i => `<tr><td><strong>${esc(i.name)}</strong> <span style="font-size:.68rem;color:var(--text-muted)">${esc(i.code)} · ${esc(i.stallCode)}${i.productDeleted ? ' · ' + esc(t('ProductRemoved')) : ''}</span></td>
          <td>${i.quantity} × ${money(i.unitPrice)}</td><td><strong>${money(i.lineTotal)}</strong></td></tr>`).join('')}
          <tr><td><strong>${esc(t('Total'))}</strong></td><td>${e.quantity}</td><td><strong style="color:var(--success)">${money(e.total)}</strong></td></tr>
        </tbody></table></div>
      ${e.remarks ? `<div style="margin-top:10px;font-size:.8rem">${esc(e.remarks)}</div>` : ''}`;
    if (typeof openModal === 'function') openModal('saleViewModal');
  }

  function editEntry(entryNo){
    const e = entries.find(x => x.entryNo === entryNo);
    if (!e){ toast(t('Error'), t('SaleNotFound'), 'error'); return; }
    if (dirty && !confirm(t('DiscardChanges'))) return;
    clearErrors();
    editing = { entryNo: e.entryNo, items: e.items };
    $('salesEntryRows').innerHTML = '';
    $('salesStall').value = '';      // show every product so the entry's own products are selectable
    e.items.forEach(i => addRow({ code: i.code, quantity: i.quantity, unitPrice: i.unitPrice }));
    $('salesDate').value = e.date;
    $('salesTime').value = e.time;
    $('salesRemarks').value = e.remarks || '';
    $('entryFormTitle').textContent = t('EditingEntry', e.entryNo);
    $('saveSalesText').textContent = t('UpdateSalesEntry');
    $('cancelEditBtn').style.display = '';
    dirty = false;
    updateTotals();
    $('salesEntryForm').scrollIntoView({ behavior: 'smooth', block: 'start' });
  }

  async function deleteEntry(entryNo){
    const e = entries.find(x => x.entryNo === entryNo);
    if (!e){ toast(t('Error'), t('SaleNotFound'), 'error'); return; }
    if (!confirm(t('ConfirmDeleteSale', e.entryNo, money(e.total)))) return;
    try {
      const res = await fetch('/Sales/Delete/' + encodeURIComponent(entryNo), { method: 'POST', headers: { 'RequestVerificationToken': token() } });
      if (res.status === 404){ toast(t('Error'), t('SaleNotFound'), 'error'); await refreshAll(); return; }
      if (!res.ok){ toast(t('Error'), `${t('CheckForm')} (${res.status})`, 'error'); return; }
      toast(t('Success'), t('SalesDeleted', entryNo), 'success');
      if (editing?.entryNo === entryNo) resetForm(true);
      await refreshAll();
    } catch (err) {
      console.error(err);
      toast(t('Error'), t('CouldNotReachServer'), 'error');
    }
  }

  // ── Product-wise report ───────────────────────────────────────
  function reportFilters(){
    return { q: $('reportSearch').value.trim(), categoryId: $('reportCategory').value, stall: $('reportStall').value, from: $('reportFrom').value, to: $('reportTo').value };
  }
  async function loadReport(){
    const seq = ++reportSeq;
    const err = $('report_err');
    err.textContent = ''; err.style.display = '';
    const f = reportFilters();
    if (f.from && f.to && f.from > f.to){ err.textContent = t('ValDateRange'); err.style.display = 'block'; return; }
    try {
      const res = await fetch('/Sales/ProductReport?' + qs(f));
      if (seq !== reportSeq) return;
      if (res.status === 400){
        const json = await res.json().catch(() => ({}));
        err.textContent = Object.values(json.errors || {})[0] || t('CheckForm'); err.style.display = 'block';
        return;
      }
      if (!res.ok){ console.warn('Sales: report request failed', res.status); return; }
      const json = await res.json();
      if (seq !== reportSeq) return;
      renderReport(json);
    } catch (e) {
      console.error(e);
      toast(t('Error'), t('CouldNotReachServer'), 'error');
    }
  }
  function renderReport(json){
    const body = $('productSalesBody');
    const rows = json.rows || [];
    $('reportTotals').textContent = rows.length ? `${t('ProductsCount', json.totals.products)} · ${t('UnitsSold', json.totals.quantity)} · ${money(json.totals.revenue)}` : '';
    if (!rows.length){ body.innerHTML = `<tr><td colspan="9" class="empty-state">${esc(t('NoSalesForFilters'))}</td></tr>`; return; }
    body.innerHTML = rows.map((r, i) => {
      const stock = r.stockLeft == null
        ? `<span class="badge badge-info">${esc(t('ProductRemoved'))}</span>`
        : `<span class="badge ${r.stockLeft <= 0 ? 'badge-danger' : r.stockLeft < r.minStock ? 'badge-warning' : 'badge-success'}">${r.stockLeft}</span>`;
      return `<tr>
        <td>${i + 1}</td>
        <td><strong>${esc(r.name)}</strong> <span style="font-size:.68rem;color:var(--text-muted)">${esc(r.code)}</span></td>
        <td><span class="badge badge-info">${esc(r.categoryName || '—')}</span></td>
        <td>${esc(r.stallCode)}</td>
        <td><strong>${r.quantity}</strong></td>
        <td>${money(r.unitPrice)}</td>
        <td><strong style="color:var(--success)">${money(r.revenue)}</strong></td>
        <td>${stock}</td>
        <td>${esc(formatDate(r.date))}</td>
      </tr>`;
    }).join('');
  }

  // ── Charts ────────────────────────────────────────────────────
  function renderCharts(json){
    if (typeof Chart === 'undefined') return;
    Chart.defaults.font.family = "'Poppins', sans-serif";
    const isToday = json.date === todayIso();
    $('catChartTitle').textContent = isToday ? t('SalesByCategoryToday') : t('SalesByCategoryOn', formatDate(json.date));

    const colors = ['#2558a8','#e85d04','#1b7a3e','#6366f1','#0369a1','#b45309','#be123c','#0891b2','#7c3aed','#059669'];
    const cat = json.categoryChart;
    if (catChart) catChart.destroy();
    catChart = new Chart($('salesCatChart'), {
      type: 'bar',
      data: { labels: cat.labels, datasets: [{ label: t('SalesAmount'), data: cat.values, backgroundColor: cat.labels.map((_, i) => colors[i % colors.length]), borderRadius: 5, borderSkipped: false }] },
      options: {
        responsive: true, maintainAspectRatio: false,
        plugins: { legend: { display: false }, tooltip: { callbacks: { label: c => money(c.parsed.y) } } },
        scales: { x: { grid: { display: false } }, y: { beginAtZero: true, ticks: { callback: shortMoney } } }
      }
    });

    const hourly = json.hourlyChart;
    if (hourlyChart) hourlyChart.destroy();
    hourlyChart = new Chart($('hourlySalesChart'), {
      type: 'line',
      data: {
        labels: hourly.hours.map(hourLabel),
        datasets: [{ label: t('SalesAmount'), data: hourly.values, borderColor: '#2558a8', backgroundColor: 'rgba(37,88,168,.1)', fill: true, tension: .4, borderWidth: 2.5, pointBackgroundColor: '#2558a8', pointRadius: 3 }]
      },
      options: {
        responsive: true, maintainAspectRatio: false,
        plugins: { legend: { display: false }, tooltip: { callbacks: { label: c => money(c.parsed.y) } } },
        scales: { x: { grid: { display: false } }, y: { beginAtZero: true, ticks: { callback: shortMoney } } }
      }
    });
  }

  // ── Auto stock deduction log ──────────────────────────────────
  async function loadDeductions(){
    const el = $('stockDeductList');
    const scope = $('deductScope').value;
    try {
      const res = await fetch('/Sales/DeductionLog' + (scope === 'day' ? '?date=' + encodeURIComponent(selectedDate()) : ''));
      if (!res.ok){ console.warn('Sales: deduction log request failed', res.status); return; }
      const rows = await res.json();
      if (!rows.length){ el.innerHTML = `<div class="empty-state">${esc(t('NoDeductions'))}</div>`; return; }
      el.innerHTML = rows.map(d => {
        const restored = d.type === 'SALE_REVERSAL';
        const pct = d.before > 0 ? Math.max(0, Math.min(100, Math.round(d.after / d.before * 100))) : 0;
        const low = d.status && d.status !== 'IN_STOCK';
        return `<div class="stock-deduct-row">
          <div class="stock-deduct-icon${restored ? ' restored' : ''}"><i class="fa-solid ${restored ? 'fa-plus' : 'fa-minus'}"></i></div>
          <div style="flex:1;min-width:180px">
            <div style="font-weight:600;color:var(--text)">${esc(d.name)} <span style="font-size:.68rem;color:var(--text-muted)">${esc(d.code)}${d.productDeleted ? ' · ' + esc(t('ProductRemoved')) : ''}</span></div>
            <div style="font-size:.72rem;color:var(--text-muted)">${d.stallCode ? esc(d.stallCode) + ' &nbsp;|&nbsp; ' : ''}${esc(t('EntryNo'))}: ${esc(d.entryNo || '—')} &nbsp;|&nbsp; ${esc(t(restored ? 'UnitsRestored' : 'UnitsSold', d.quantity))} &nbsp;|&nbsp; ${esc(formatDate(d.date))}</div>
          </div>
          <div style="text-align:center;min-width:100px">
            <div style="font-size:.72rem;color:var(--text-muted)">${esc(t('BeforeAfter'))}</div>
            <div style="font-weight:700">${d.before} → <span style="color:${low ? 'var(--danger)' : 'var(--success)'}">${d.after}</span></div>
          </div>
          ${restored ? '<div style="min-width:90px"></div>' : `<div style="min-width:90px">
            <div class="deduct-bar"><div class="deduct-fill" style="width:${pct}%;background:${low ? 'var(--danger)' : 'var(--success)'}"></div></div>
            <div style="font-size:.65rem;color:var(--text-muted);text-align:right;margin-top:2px">${esc(t('PercentRemaining', pct))}</div>
          </div>`}
          ${d.status ? `<span class="badge ${statusBadge(d.status)}" style="font-size:.65rem">${esc(statusLabel(d.status))}</span>` : ''}
          <span class="badge ${restored ? 'badge-info' : 'badge-success'}" style="font-size:.65rem">${esc(t(restored ? 'Restored' : 'Deducted'))}</span>
        </div>`;
      }).join('');
    } catch (e) {
      console.error(e);
    }
  }

  async function refreshAll(){
    await load();
    loadReport();
    loadDeductions();
  }

  // ── Wiring ────────────────────────────────────────────────────
  function debounce(fn, ms){ let h; return (...a) => { clearTimeout(h); h = setTimeout(() => fn(...a), ms); }; }

  document.addEventListener('DOMContentLoaded', () => {
    const title = $('entryFormTitle');
    title.dataset.title = title.textContent;
    $('salesDate').value = todayIso();
    $('salesDate').max = todayIso();
    $('salesTime').value = nowTime();

    $('salesDate').addEventListener('change', () => {
      if (!editing) $('salesTime').value = $('salesDate').value === todayIso() ? nowTime() : '12:00';
      load(); loadDeductions();
    });
    $('salesStall').addEventListener('change', load);
    $('addRowBtn').addEventListener('click', () => { addRow(); dirty = true; });
    $('clearRowsBtn').addEventListener('click', () => {
      if (dirty && !confirm(t('DiscardChanges'))) return;
      resetForm(true);
      toast(t('Success'), t('SalesEntryCleared'), 'info');
    });
    $('cancelEditBtn').addEventListener('click', () => {
      if (dirty && !confirm(t('DiscardChanges'))) return;
      resetForm(true);
    });
    $('saveSalesBtn').addEventListener('click', save);
    $('salesEntryForm').addEventListener('submit', e => { e.preventDefault(); save(); });

    // "Submit Today's Sales": saves the entry form for today
    $('submitTodayBtn').addEventListener('click', () => {
      document.querySelector('#salesTabs .tab-item[data-tab="daily"]')?.click();
      if (!editing && $('salesDate').value !== todayIso()){
        $('salesDate').value = todayIso();
        $('salesTime').value = nowTime();
        load();
      }
      save();
    });

    // header Export: entries of the selected date (and stall)
    $('salesExportBtn').addEventListener('click', () => {
      const d = selectedDate();
      window.location.href = '/Sales/Export?' + qs({ from: d, to: d, stall: selectedStall() });
    });

    $('salesEntriesBody').addEventListener('click', e => {
      const btn = e.target.closest('button[data-act]'); if (!btn) return;
      const id = btn.dataset.id;
      if (btn.dataset.act === 'view') viewEntry(id);
      else if (btn.dataset.act === 'edit') editEntry(id);
      else if (btn.dataset.act === 'delete') deleteEntry(id);
    });

    // report filters (default: last 30 days)
    const from = new Date(); from.setDate(from.getDate() - 29);
    $('reportFrom').value = isoOf(from);
    $('reportTo').value = todayIso();
    $('reportSearch').addEventListener('input', debounce(loadReport, 250));
    ['reportCategory', 'reportStall', 'reportFrom', 'reportTo'].forEach(id => $(id).addEventListener('change', loadReport));
    $('reportResetBtn').addEventListener('click', () => {
      $('reportSearch').value = ''; $('reportCategory').value = ''; $('reportStall').value = '';
      $('reportFrom').value = isoOf(from); $('reportTo').value = todayIso();
      loadReport();
    });
    $('reportExportBtn').addEventListener('click', () => {
      const f = reportFilters();
      if (f.from && f.to && f.from > f.to){ toast(t('Error'), t('ValDateRange'), 'error'); return; }
      window.location.href = '/Sales/ExportProductReport?' + qs(f);
    });

    $('deductScope').addEventListener('change', loadDeductions);

    window.addEventListener('beforeunload', e => { if (dirty) { e.preventDefault(); e.returnValue = ''; } });

    addRow();
    refreshAll();
  });
})();
