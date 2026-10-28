// Inventory page: products, categories, stock updates and low-stock alerts through the /Inventory endpoints.
// Localized strings come from window.InventoryI18n (rendered by Views/Inventory/Index.cshtml from SharedResource.*.resx).
(function InventoryModule(){
  let products = [];        // current (filtered) product list
  let productOptions = [];  // all products, for the stock update picker
  let categories = [];
  let lowStock = [];
  let movements = [];
  let formDirty = false;
  let saving = false;
  let loadSeq = 0;          // ignores out-of-order responses when filters change quickly

  // ── i18n / formatting ─────────────────────────────────────────
  const I18N = window.InventoryI18n || {};
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
  function statusLabel(s){ return s === 'OUT_OF_STOCK' ? t('OutOfStock') : s === 'LOW_STOCK' ? t('LowStock') : t('InStock'); }
  function statusBadge(s){ return s === 'IN_STOCK' ? 'badge-success' : s === 'LOW_STOCK' ? 'badge-warning' : 'badge-danger'; }
  function typeLabel(type){
    return { INITIAL: t('InitialStock'), ADD: t('AddStock'), REDUCE: t('ReduceStock'), CORRECTION: t('Correction'),
      SALE: t('SaleDeduction'), SALE_REVERSAL: t('SaleReversal') }[type] || type;
  }
  function formatDate(iso){
    const m = /^(\d{4})-(\d{2})-(\d{2})$/.exec(iso || '');
    if (!m) return iso || '—';
    const d = new Date(+m[1], +m[2] - 1, +m[3]);
    return `${m[3]} ${d.toLocaleString(lang(), { month: 'short' })} ${m[1]}`;
  }
  function todayIso(){
    const d = new Date();
    return `${d.getFullYear()}-${String(d.getMonth()+1).padStart(2,'0')}-${String(d.getDate()).padStart(2,'0')}`;
  }
  const token = () => document.querySelector('input[name="__RequestVerificationToken"]')?.value || '';

  // ── Rendering ─────────────────────────────────────────────────
  function renderStats(s){
    const set = (id, v) => { const el = document.getElementById(id); if (el) el.textContent = v; };
    set('statTotalProducts', s.total);
    set('statInStock', s.inStock);
    set('statLowStock', s.belowMinimum);
    set('statCategories', s.categories);
    set('statStalls', s.stallsTracked);
    set('tabCountAll', s.total);
    set('tabCountAlerts', s.belowMinimum);
    set('belowMinCount', t('ItemsCount', s.belowMinimum));
    const alert = document.getElementById('belowMinAlert');
    if (alert) alert.style.display = s.belowMinimum ? '' : 'none';
    // sidebar badge (rendered by _Sidebar.cshtml) follows stock changes without a reload
    const badge = document.getElementById('navInventoryBadge');
    if (badge){ badge.textContent = s.belowMinimum; badge.style.display = s.belowMinimum ? '' : 'none'; }
  }

  function renderProducts(){
    const body = document.getElementById('inventoryBody');
    if (!body) return;
    body.innerHTML = products.map((p, i) => `
      <tr>
        <td>${i + 1}</td>
        <td><div style="display:flex;align-items:center;gap:10px"><div class="product-thumb"><i class="fa-solid fa-box"></i></div><div><strong>${esc(p.name)}</strong><div style="font-size:.68rem;color:var(--text-muted)">${esc(p.code)}</div></div></div></td>
        <td>${esc(p.categoryName)}</td><td>${esc(p.stallCode)}</td>
        <td><strong>${p.quantity}</strong></td><td>${p.minStock}</td><td>${esc(money(p.unitPrice))}</td>
        <td><span class="badge ${statusBadge(p.status)}">${esc(statusLabel(p.status))}</span></td>
        <td style="white-space:nowrap">
          <button type="button" class="btn btn-ghost btn-sm btn-icon" data-edit-product="${esc(p.code)}" title="${esc(t('Edit'))}"><i class="fa-solid fa-pen"></i></button>
          <button type="button" class="btn btn-ghost btn-sm btn-icon" data-restock-product="${esc(p.code)}" title="${esc(t('Restock'))}"><i class="fa-solid fa-rotate"></i></button>
          <button type="button" class="btn btn-ghost btn-sm btn-icon" data-delete-product="${esc(p.code)}" title="${esc(t('Delete'))}" style="color:var(--danger)"><i class="fa-solid fa-trash"></i></button>
        </td>
      </tr>`).join('') || `<tr><td colspan="9" style="text-align:center;color:var(--text-muted);padding:18px">${esc(t('NoProductsFound'))}</td></tr>`;
  }

  function renderCategoryCards(){
    const el = document.getElementById('catCards');
    if (!el) return;
    el.innerHTML = categories.map(c => `
      <div class="card"><div class="card-body">
        <div style="font-size:.85rem;font-weight:700;color:var(--primary);margin-bottom:10px">${esc(c.name)}</div>
        <div style="font-size:1.4rem;font-weight:800">${c.productCount} <span style="font-size:.75rem;font-weight:500;color:var(--text-muted)">${esc(t('ProductsLabel'))}</span></div>
        <div style="font-size:.72rem;color:var(--text-muted);margin-top:4px">${esc(t('TotalUnits'))}: <strong>${c.totalQuantity}</strong> &nbsp;|&nbsp; ${esc(t('StockValue'))}: <strong>${esc(money(c.stockValue))}</strong></div>
        ${c.lowCount
          ? `<div style="font-size:.72rem;color:var(--danger);margin-top:4px"><i class="fa-solid fa-triangle-exclamation"></i> ${esc(t('LowStockCount', c.lowCount))}</div>`
          : `<div style="font-size:.72rem;color:var(--success);margin-top:4px"><i class="fa-solid fa-circle-check"></i> ${esc(t('AllStocked'))}</div>`}
        <button type="button" class="btn btn-outline btn-sm" style="margin-top:10px;width:100%" data-view-category="${esc(c.id)}">${esc(t('ViewProducts'))}</button>
      </div></div>`).join('');
  }

  function renderLowStock(){
    const list = document.getElementById('lowStockCardList');
    if (list){
      list.innerHTML = lowStock.map(p => `
        <div class="alert-list-item stock-low" style="padding:8px 0">
          <div class="alert-list-icon" style="width:28px;height:28px;font-size:.7rem"><i class="fa-solid fa-box-open"></i></div>
          <div class="alert-list-info"><div class="alert-list-name" style="font-size:.75rem">${esc(p.name)}</div><div class="alert-list-qty">${esc(t('Quantity'))}: ${p.quantity} / ${esc(t('MinStock'))}: ${p.minStock}</div></div>
        </div>`).join('') || `<div style="font-size:.75rem;color:var(--success);padding:12px 0"><i class="fa-solid fa-circle-check"></i> ${esc(t('NoLowStock'))}</div>`;
    }
    const body = document.getElementById('alertsBody');
    if (body){
      body.innerHTML = lowStock.map(p => `
        <tr>
          <td><strong>${esc(p.name)}</strong><div style="font-size:.68rem;color:var(--text-muted)">${esc(p.code)}</div></td><td>${esc(p.categoryName)}</td><td>${esc(p.stallCode)}</td>
          <td><strong style="color:var(--danger)">${p.quantity}</strong></td><td>${p.minStock}</td>
          <td><span class="badge ${p.urgency === 'CRITICAL' ? 'badge-danger' : 'badge-warning'}">${esc(p.urgency === 'CRITICAL' ? t('Critical') : t('Moderate'))}</span></td>
          <td><button type="button" class="btn btn-primary btn-sm" data-restock-product="${esc(p.code)}"><i class="fa-solid fa-cart-plus"></i> ${esc(t('Restock'))}</button></td>
        </tr>`).join('') || `<tr><td colspan="7" style="text-align:center;color:var(--success);padding:18px">${esc(t('NoLowStock'))}</td></tr>`;
    }
  }

  function renderMovements(){
    const body = document.getElementById('movementsBody');
    if (!body) return;
    body.innerHTML = movements.map(m => {
      const delta = m.after - m.before;
      const deltaStr = delta > 0 ? `+${delta}` : `${delta}`;
      return `<tr>
        <td>${esc(formatDate(m.date))}</td>
        <td><strong>${esc(m.name)}</strong> <span style="font-size:.68rem;color:var(--text-muted)">${esc(m.code)}${m.productDeleted ? ' · ' + esc(t('ProductRemoved')) : ''}</span></td>
        <td>${esc(typeLabel(m.type))}</td>
        <td><strong style="color:${delta < 0 ? 'var(--danger)' : delta > 0 ? 'var(--success)' : 'var(--text)'}">${esc(deltaStr)}</strong></td>
        <td>${m.before}</td><td><strong>${m.after}</strong></td>
        <td>${esc(m.remarks || '—')}</td><td>${esc(m.by || '—')}</td>
      </tr>`;
    }).join('') || `<tr><td colspan="8" style="text-align:center;color:var(--text-muted);padding:18px">${esc(t('NoStockUpdates'))}</td></tr>`;
  }

  function renderSelects(stalls){
    const opts = categories.map(c => `<option value="${esc(c.id)}">${esc(c.name)}</option>`).join('');
    const filter = document.getElementById('productFilterCategory');
    if (filter){ const cur = filter.value; filter.innerHTML = `<option value="">${esc(t('AllCategories'))}</option>` + opts; filter.value = cur; }
    const formCat = document.getElementById('productCategory');
    if (formCat){ const cur = formCat.value; formCat.innerHTML = `<option value="">${esc(t('SelectPlaceholder'))}</option>` + opts; formCat.value = cur; }
    const dl = document.getElementById('stallOptions');
    if (dl) dl.innerHTML = (stalls || []).map(s => `<option value="${esc(s)}">`).join('');
    const su = document.getElementById('suProduct');
    if (su){
      const cur = su.value;
      su.innerHTML = `<option value="">-- ${esc(t('SearchSelect'))} --</option>` +
        productOptions.map(p => `<option value="${esc(p.code)}">${esc(p.name)} (${esc(p.stallCode)}) — ${p.quantity}</option>`).join('');
      su.value = cur;
      updateStockPreview();
    }
  }

  function renderChart(chart){
    if (!window.IMSBCharts || typeof IMSBCharts.initStockLevelChart !== 'function' || !chart) return;
    const canvas = document.getElementById('stockLevelChart');
    IMSBCharts.initStockLevelChart('stockLevelChart', {
      labels: chart.labels, current: chart.current, minimum: chart.minimum,
      currentLabel: canvas?.dataset.labelCurrent, minimumLabel: canvas?.dataset.labelMin
    });
  }

  // ── Data ──────────────────────────────────────────────────────
  function currentFilters(){
    const val = id => (document.getElementById(id)?.value || '').trim();
    return { q: val('productSearch'), categoryId: val('productFilterCategory'), status: val('productFilterStatus') };
  }
  function queryString(params){
    return Object.keys(params).filter(k => params[k]).map(k => encodeURIComponent(k) + '=' + encodeURIComponent(params[k])).join('&');
  }

  async function load(){
    const seq = ++loadSeq;
    try {
      const qs = queryString(currentFilters());
      const res = await fetch('/Inventory/Data' + (qs ? '?' + qs : ''));
      if (!res.ok){ console.warn('Inventory: data request failed', res.status); return false; }
      const json = await res.json();
      if (seq !== loadSeq) return true;   // a newer request will render
      products = json.products || [];
      productOptions = json.productOptions || [];
      categories = json.categories || [];
      lowStock = json.lowStock || [];
      movements = json.movements || [];
      renderStats(json.stats);
      renderSelects(json.stalls);
      renderProducts();
      renderCategoryCards();
      renderLowStock();
      renderMovements();
      renderChart(json.chart);
      return true;
    } catch (err){
      console.warn('Inventory: load error', err);
      return false;
    }
  }

  // ── Errors (forms.css only shows .form-error inside .form-group.has-error) ──
  function clearErrors(form){
    form.querySelectorAll('.form-group.has-error').forEach(g => g.classList.remove('has-error'));
    form.querySelectorAll('.form-error').forEach(e => { e.textContent = ''; });
  }
  function showErrors(prefix, errors){
    let first = null;
    Object.keys(errors).forEach(k => {
      const el = document.getElementById(prefix + k);
      if (!el) return;
      el.textContent = errors[k];
      const group = el.closest('.form-group');
      group?.classList.add('has-error');
      if (!first) first = group?.querySelector('input,select,textarea');
    });
    first?.focus();
  }

  // ── Product modal ─────────────────────────────────────────────
  const modalEl = () => document.getElementById('addProductModal');
  function openModal(){
    const o = modalEl();
    o.style.display = 'flex';
    o.classList.add('open');            // .open makes the overlay visible and clickable (style.css)
    o.setAttribute('aria-hidden', 'false');
    setTimeout(() => document.getElementById('productName')?.focus(), 50);
    window.addEventListener('keydown', escHandler, true);
  }
  function closeModal(){
    const o = modalEl();
    o.classList.remove('open');
    o.style.display = 'none';
    o.setAttribute('aria-hidden', 'true');
    window.removeEventListener('keydown', escHandler, true);
    clearErrors(document.getElementById('productForm'));
  }
  function escHandler(e){ if (e.key !== 'Escape') return; e.stopPropagation(); tryClose(); }
  function tryClose(){ if (formDirty && !confirm(t('DiscardChanges'))) return; formDirty = false; closeModal(); }

  function setQuantityMode(editing){
    const qty = document.getElementById('productQuantity');
    const label = document.getElementById('productQuantityLabel');
    label.textContent = editing ? label.dataset.edit : label.dataset.add;
    // after creation, quantity only changes through audited stock updates
    qty.disabled = editing;
    document.getElementById('productQuantityReq').style.display = editing ? 'none' : '';
    document.getElementById('productQuantityHint').style.display = editing ? '' : 'none';
  }

  function openCreate(){
    formDirty = false;
    const form = document.getElementById('productForm');
    form.reset();
    clearErrors(form);
    const m = modalEl().querySelector('.modal');
    document.getElementById('productModalTitle').textContent = m.dataset.addTitle;
    document.getElementById('productSubmitText').textContent = m.dataset.addText;
    document.getElementById('productCode').value = '';
    document.getElementById('productQuantity').value = '0';
    document.getElementById('productMinStock').value = '0';
    document.getElementById('productPrice').value = '0';
    setQuantityMode(false);
    openModal();
  }

  function openEdit(code){
    const p = products.find(x => x.code === code);
    if (!p){ showToast(t('Error'), t('ProductNotFound'), 'error'); return; }
    formDirty = false;
    const form = document.getElementById('productForm');
    clearErrors(form);
    const m = modalEl().querySelector('.modal');
    document.getElementById('productModalTitle').textContent = `${m.dataset.editTitle} — ${p.code}`;
    document.getElementById('productSubmitText').textContent = m.dataset.editText;
    document.getElementById('productCode').value = p.code;
    document.getElementById('productName').value = p.name;
    document.getElementById('productCategory').value = p.categoryId;
    document.getElementById('productStall').value = p.stallCode;
    document.getElementById('productQuantity').value = p.quantity;
    document.getElementById('productMinStock').value = p.minStock;
    document.getElementById('productPrice').value = p.unitPrice;
    setQuantityMode(true);
    openModal();
  }

  function intValue(id){ const v = document.getElementById(id).value.trim(); return v === '' ? NaN : Number(v); }

  function validateProduct(v, editing){
    const e = {};
    if (v.name.length < 2 || v.name.length > 200) e.name = t('ValProductName');
    if (!v.categoryId) e.category_id = t('ValCategory');
    if (!/^[A-Za-z0-9-]{1,20}$/.test(v.stallCode)) e.stall_code = t('ValStall');
    if (!editing && !(Number.isInteger(v.quantity) && v.quantity >= 0)) e.quantity = t('ValQuantity');
    if (!(Number.isInteger(v.minStock) && v.minStock >= 0)) e.min_stock = t('ValMinStock');
    if (!(Number.isFinite(v.unitPrice) && v.unitPrice >= 0)) e.unit_price = t('ValUnitPrice');
    return e;
  }

  async function saveProduct(e){
    if (e) e.preventDefault();
    if (saving) return;
    const form = document.getElementById('productForm');
    clearErrors(form);
    const code = document.getElementById('productCode').value;
    const editing = !!code;
    const v = {
      name: document.getElementById('productName').value.trim(),
      categoryId: document.getElementById('productCategory').value,
      stallCode: document.getElementById('productStall').value.trim().toUpperCase(),
      quantity: intValue('productQuantity'),
      minStock: document.getElementById('productMinStock').value.trim() === '' ? 0 : intValue('productMinStock'),
      unitPrice: document.getElementById('productPrice').value.trim() === '' ? 0 : Number(document.getElementById('productPrice').value)
    };
    const errors = validateProduct(v, editing);
    if (Object.keys(errors).length){ showErrors('err_', errors); return; }

    const body = editing
      ? { name: v.name, categoryId: v.categoryId, stallCode: v.stallCode, minStock: v.minStock, unitPrice: v.unitPrice }
      : v;
    const btn = document.getElementById('productSubmitBtn');
    saving = true; btn.disabled = true;
    try {
      const res = await fetch(editing ? '/Inventory/Update/' + encodeURIComponent(code) : '/Inventory/Create', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json', 'RequestVerificationToken': token() },
        body: JSON.stringify(body)
      });
      if (res.status === 400){
        const json = await res.json().catch(() => null);
        if (json && json.errors){ showErrors('err_', json.errors); return; }
        showToast(t('Error'), t('CheckForm'), 'error'); return;
      }
      if (res.status === 404){ showToast(t('Error'), t('ProductNotFound'), 'error'); return; }
      if (!res.ok){ showToast(t('Error'), `${t('CheckForm')} (${res.status})`, 'error'); return; }
      showToast(t('Success'), editing ? t('ProductUpdated') : t('ProductAdded'), 'success');
      formDirty = false;
      closeModal();
      await load();
    } catch (err){
      console.error('Inventory: save error', err);
      showToast(t('Error'), t('CouldNotReachServer'), 'error');
    } finally {
      saving = false; btn.disabled = false;
    }
  }

  async function deleteProduct(code){
    const p = products.find(x => x.code === code);
    if (!p){ showToast(t('Error'), t('ProductNotFound'), 'error'); return; }
    if (!confirm(t('ConfirmDeleteProduct', p.name, p.code))) return;
    try {
      const res = await fetch('/Inventory/Delete/' + encodeURIComponent(code), { method: 'POST', headers: { 'RequestVerificationToken': token() } });
      if (res.status === 404){ showToast(t('Error'), t('ProductNotFound'), 'error'); await load(); return; }
      if (!res.ok){ showToast(t('Error'), `${t('CheckForm')} (${res.status})`, 'error'); return; }
      showToast(t('Success'), t('ProductDeletedMsg'), 'success');
      await load();
    } catch (err){
      console.error('Inventory: delete error', err);
      showToast(t('Error'), t('CouldNotReachServer'), 'error');
    }
  }

  // ── Stock update ──────────────────────────────────────────────
  function switchTab(name){
    document.querySelector(`#inventoryTabs .tab-item[data-tab="${name}"]`)?.click();
  }

  // Restock buttons jump to the Stock Update tab with the product and "Add Stock" preselected
  function startRestock(code){
    switchTab('update');
    resetStockForm();
    document.getElementById('suProduct').value = code;
    document.getElementById('suType').value = 'ADD';
    updateStockPreview();
    document.getElementById('suQuantity').focus();
    document.getElementById('stockUpdateForm').scrollIntoView({ behavior: 'smooth', block: 'start' });
  }

  function resetStockForm(){
    const form = document.getElementById('stockUpdateForm');
    form.reset();
    clearErrors(form);
    document.getElementById('suDate').value = todayIso();
    updateStockPreview();
  }

  // Shows the available quantity and what the stock will become, mirroring StockMovementTypes.Apply
  function updateStockPreview(){
    const code = document.getElementById('suProduct')?.value;
    const p = productOptions.find(x => x.code === code);
    const avail = document.getElementById('suAvailable');
    const preview = document.getElementById('suPreview');
    if (!avail || !preview) return;
    avail.textContent = p ? t('AvailableQty', p.quantity) : '';
    const type = document.getElementById('suType').value;
    const qty = intValue('suQuantity');
    preview.style.color = 'var(--text-muted)';
    if (type === 'CORRECTION' && !Number.isInteger(qty)) { preview.textContent = t('CorrectionHint'); return; }
    if (!p || !type || !Number.isInteger(qty)) { preview.textContent = ''; return; }
    const after = type === 'ADD' ? p.quantity + qty : type === 'REDUCE' ? p.quantity - qty : qty;
    preview.textContent = t('NewQuantityPreview', after);
    if (after < 0) preview.style.color = 'var(--danger)';
  }

  function validateStock(v){
    const e = {};
    const p = productOptions.find(x => x.code === v.productCode);
    if (!p) e.product_code = t('ValSelectProduct');
    if (!['ADD','REDUCE','CORRECTION'].includes(v.updateType)) e.update_type = t('ValUpdateType');
    if (!Number.isInteger(v.quantity) || v.quantity < 0) e.quantity = t('ValQuantity');
    else if (v.updateType !== 'CORRECTION' && v.quantity < 1) e.quantity = t('ValQuantityPositive');
    else if (p && v.updateType === 'REDUCE' && v.quantity > p.quantity) e.quantity = t('ValInsufficientStock', p.quantity);
    if (!v.date) e.date = t('ValDate');
    else if (v.date > todayIso()) e.date = t('ValDateFuture');
    if (v.remarks.length > 500) e.remarks = t('ValRemarks');
    return e;
  }

  async function submitStock(e){
    if (e) e.preventDefault();
    if (saving) return;
    const form = document.getElementById('stockUpdateForm');
    clearErrors(form);
    const v = {
      productCode: document.getElementById('suProduct').value,
      updateType: document.getElementById('suType').value,
      quantity: intValue('suQuantity'),
      date: document.getElementById('suDate').value,
      remarks: document.getElementById('suRemarks').value.trim()
    };
    const errors = validateStock(v);
    if (Object.keys(errors).length){ showErrors('su_err_', errors); return; }
    const btn = document.getElementById('suSubmitBtn');
    saving = true; btn.disabled = true;
    try {
      const res = await fetch('/Inventory/UpdateStock', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json', 'RequestVerificationToken': token() },
        body: JSON.stringify({ ...v, remarks: v.remarks || null })
      });
      if (res.status === 400){
        const json = await res.json().catch(() => null);
        if (json && json.errors){ showErrors('su_err_', json.errors); return; }
        showToast(t('Error'), t('CheckForm'), 'error'); return;
      }
      if (!res.ok){ showToast(t('Error'), `${t('CheckForm')} (${res.status})`, 'error'); return; }
      showToast(t('Success'), t('StockUpdatedSuccessfully'), 'success');
      resetStockForm();
      await load();
    } catch (err){
      console.error('Inventory: stock update error', err);
      showToast(t('Error'), t('CouldNotReachServer'), 'error');
    } finally {
      saving = false; btn.disabled = false;
    }
  }

  function exportCsv(){
    const qs = queryString(currentFilters());
    window.location = '/Inventory/Export' + (qs ? '?' + qs : '');
  }

  function debounce(fn, wait){ let h; return (...a) => { clearTimeout(h); h = setTimeout(() => fn(...a), wait); }; }

  window.InventoryModule = { load, openCreate, openEdit, startRestock };

  window.addEventListener('beforeunload', e => { if (formDirty){ e.preventDefault(); e.returnValue = ''; } });

  document.addEventListener('DOMContentLoaded', () => {
    const productForm = document.getElementById('productForm');
    if (!productForm) return;
    const overlay = modalEl();
    overlay.style.display = 'none';
    // backdrop click runs the dirty check instead of main.js's unconditional close
    overlay.addEventListener('click', e => { if (e.target !== overlay) return; e.stopImmediatePropagation(); tryClose(); }, true);

    document.getElementById('addProductBtn').addEventListener('click', openCreate);
    document.getElementById('productSubmitBtn').addEventListener('click', saveProduct);
    productForm.addEventListener('submit', saveProduct);
    productForm.addEventListener('input', () => { formDirty = true; });
    document.getElementById('productModalClose').addEventListener('click', tryClose);
    document.getElementById('productCancelBtn').addEventListener('click', tryClose);
    document.getElementById('inventoryExportBtn').addEventListener('click', exportCsv);

    // row/card buttons are re-rendered on every load, so delegate from the page
    document.querySelector('.page-body').addEventListener('click', e => {
      const b = e.target.closest('[data-edit-product],[data-delete-product],[data-restock-product],[data-view-category]');
      if (!b) return;
      if (b.dataset.editProduct) openEdit(b.dataset.editProduct);
      else if (b.dataset.deleteProduct) deleteProduct(b.dataset.deleteProduct);
      else if (b.dataset.restockProduct) startRestock(b.dataset.restockProduct);
      else if (b.dataset.viewCategory){
        document.getElementById('productFilterCategory').value = b.dataset.viewCategory;
        switchTab('all');
        load();
      }
    });

    document.getElementById('productSearch').addEventListener('input', debounce(load, 300));
    document.getElementById('productFilterCategory').addEventListener('change', load);
    document.getElementById('productFilterStatus').addEventListener('change', load);
    document.getElementById('productFilterResetBtn').addEventListener('click', () => {
      document.getElementById('productSearch').value = '';
      document.getElementById('productFilterCategory').value = '';
      document.getElementById('productFilterStatus').value = '';
      load();
    });

    const stockForm = document.getElementById('stockUpdateForm');
    stockForm.addEventListener('submit', submitStock);
    document.getElementById('suResetBtn').addEventListener('click', resetStockForm);
    ['suProduct', 'suType', 'suQuantity'].forEach(id => {
      document.getElementById(id).addEventListener('input', updateStockPreview);
      document.getElementById(id).addEventListener('change', updateStockPreview);
    });
    document.getElementById('suDate').value = todayIso();
    document.getElementById('suDate').setAttribute('max', todayIso());

    load();
  });
})();
