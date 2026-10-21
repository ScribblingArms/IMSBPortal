// Stall Management: map, list + filters, create/edit/delete, allocation and reallocation through the /Stalls endpoints.
// Localized strings come from window.StallsI18n (rendered by Views/Stalls/Index.cshtml from SharedResource.*.resx).
(function StallsModule(){
  let stalls = [];        // filtered list
  let map = [];           // every stall
  let categories = [];
  let freeArtisans = [];
  let reallocations = [];
  let loadSeq = 0;
  let saving = false;
  let formDirty = false;

  // ── i18n / formatting ─────────────────────────────────────────
  const I18N = window.StallsI18n || {};
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
  const money = v => v == null ? '—' : '₹' + Number(v).toLocaleString('en-IN', { maximumFractionDigits: 2 });
  function formatDate(iso){
    const m = /^(\d{4})-(\d{2})-(\d{2})$/.exec(iso || '');
    if (!m) return iso || '—';
    const d = new Date(+m[1], +m[2] - 1, +m[3]);
    return `${m[3]} ${d.toLocaleString(lang(), { month: 'short' })} ${m[1]}`;
  }
  function todayIso(){ const d = new Date(); return `${d.getFullYear()}-${String(d.getMonth()+1).padStart(2,'0')}-${String(d.getDate()).padStart(2,'0')}`; }
  const token = () => document.querySelector('#allocateForm input[name="__RequestVerificationToken"]')?.value || '';
  const toast = (title, msg, type) => { if (typeof showToast === 'function') showToast(title, msg, type); };
  const qs = obj => Object.entries(obj).filter(([, v]) => v !== '' && v != null).map(([k, v]) => `${k}=${encodeURIComponent(v)}`).join('&');

  const STATUS = {
    OCCUPIED: { label: () => t('Occupied'), badge: 'badge-primary', cell: 'occupied', icon: 'fa-store' },
    VACANT: { label: () => t('Vacant'), badge: 'badge-success', cell: 'vacant', icon: 'fa-door-open' },
    RESERVED: { label: () => t('Reserved'), badge: 'badge-warning', cell: 'reserved', icon: 'fa-lock' },
    MAINTENANCE: { label: () => t('UnderMaintenance'), badge: 'badge-neutral', cell: 'maintenance', icon: 'fa-screwdriver-wrench' }
  };
  const st = s => STATUS[s] || STATUS.VACANT;
  const REASON = { CLOSURE: 'ReasonClosure', CATEGORY_CHANGE: 'ReasonCategoryChange', ARTISAN_REQUEST: 'ReasonArtisanRequest',
    NON_PERFORMANCE: 'ReasonNonPerformance', AGREEMENT_EXPIRED: 'ReasonAgreementExpired', OTHER: 'ReasonOther' };
  const REQ_STATUS = { PENDING: ['PendingLabel', 'badge-warning'], APPROVED: ['ApprovedLabel', 'badge-success'], REJECTED: ['RejectedLabel', 'badge-danger'] };

  // ── Requests ──────────────────────────────────────────────────
  async function post(url, body){
    const res = await fetch(url, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json', 'RequestVerificationToken': token() },
      body: body == null ? null : JSON.stringify(body)
    });
    const json = await res.json().catch(() => ({}));
    return { res, json };
  }
  /** Shows a failed mutation: field errors in the form (prefix), anything else as a toast. Returns true when handled. */
  function failed(res, json, prefix){
    if (res.ok) return false;
    if (res.status === 404) { toast(t('Error'), t('StallNotFound'), 'error'); load(); return true; }
    const errors = json.errors || {};
    const shown = prefix ? showErrors(prefix, errors) : 0;
    const other = Object.entries(errors).filter(([k]) => !prefix || !$(prefix + k)).map(([, v]) => v);
    toast(t('Error'), other[0] || (shown ? t('CheckForm') : `${t('CheckForm')} (${res.status})`), 'error');
    return true;
  }

  // ── Load + render ─────────────────────────────────────────────
  function filters(){
    return { q: $('stallSearch').value.trim(), status: $('filterStatus').value, categoryId: $('filterCategory').value, block: $('filterBlock').value };
  }

  async function load(){
    const seq = ++loadSeq;
    try {
      const res = await fetch('/Stalls/Data?' + qs(filters()));
      if (!res.ok) { console.warn('Stalls: data request failed', res.status); return; }
      const json = await res.json();
      if (seq !== loadSeq) return;
      stalls = json.stalls || [];
      map = json.map || [];
      categories = json.categories || [];
      freeArtisans = json.freeArtisans || [];
      reallocations = json.reallocations || [];
      renderStats(json.stats);
      renderSelects(json.blocks || []);
      renderMap();
      renderList();
      renderReallocations();
    } catch (e) {
      console.error(e);
      toast(t('Error'), t('CouldNotReachServer'), 'error');
    }
  }

  function renderStats(s){
    const set = (id, v) => { const el = $(id); if (el) el.textContent = v; };
    set('statTotal', s.total); set('statOccupied', s.occupied); set('statVacant', s.vacant); set('statPending', s.pendingReallocation);
    set('tabCountMap', s.total); set('tabCountList', stalls.length); set('tabCountVacant', s.vacant); set('tabCountPending', s.pendingReallocation);
    set('legendOccupied', s.occupied); set('legendVacant', s.vacant); set('legendReserved', s.reserved); set('legendMaintenance', s.maintenance);
  }

  function fill(sel, options, first){
    if (!sel) return;
    const current = sel.value;
    sel.innerHTML = first + options.map(o => `<option value="${esc(o.value)}">${esc(o.label)}</option>`).join('');
    if ([...sel.options].some(o => o.value === current)) sel.value = current;
  }
  function renderSelects(blocks){
    const blockOpts = blocks.map(b => ({ value: b, label: t('BlockLabel', b) }));
    fill($('filterBlock'), blockOpts, $('filterBlock').options[0].outerHTML);
    fill($('mapBlock'), blockOpts, $('mapBlock').options[0].outerHTML);
    $('blockOptions').innerHTML = blocks.map(b => `<option value="${esc(b)}">`).join('');

    const catOpts = categories.map(c => ({ value: c.id, label: c.name }));
    fill($('filterCategory'), catOpts, $('filterCategory').options[0].outerHTML);
    fill($('stCategory'), catOpts, $('stCategory').options[0].outerHTML);
    fill($('alCategory'), catOpts, $('alCategory').options[0].outerHTML);

    const vacant = map.filter(s => s.status === 'VACANT').map(s => ({ value: s.code, label: `${s.code} (${t('BlockLabel', s.block)})` }));
    fill($('alStall'), vacant, `<option value="">${esc(vacant.length ? t('SelectPlaceholder') : t('NoVacantStalls'))}</option>`);
    const artisanOpts = freeArtisans.map(a => ({ value: a.code, label: `${a.name} (${a.code})${a.categoryName ? ' – ' + a.categoryName : ''}` }));
    fill($('alArtisan'), artisanOpts, `<option value="">${esc(artisanOpts.length ? t('SelectPlaceholder') : t('NoFreeArtisans'))}</option>`);
    fill($('reArtisan'), artisanOpts, `<option value="">${esc(t('VacateStallOption'))}</option>`);
    const occupied = map.filter(s => s.status === 'OCCUPIED' && !s.hasPendingReallocation)
      .map(s => ({ value: s.code, label: `${s.code} – ${s.artisanName || ''}` }));
    fill($('reStall'), occupied, `<option value="">${esc(occupied.length ? t('SelectPlaceholder') : t('NoOccupiedStalls'))}</option>`);
  }

  function renderMap(){
    const el = $('stallMap');
    const block = $('mapBlock').value;
    const shown = block ? map.filter(s => s.block === block) : map;
    if (!shown.length){ el.innerHTML = `<div class="empty-state">${esc(t('NoStallsRegistered'))}</div>`; return; }
    const byBlock = {};
    shown.forEach(s => (byBlock[s.block] = byBlock[s.block] || []).push(s));
    el.innerHTML = Object.keys(byBlock).sort().map(b => `
      <div class="block-title">${esc(t('BlockLabel', b))}</div>
      <div class="stall-map">${byBlock[b].map(s => {
        const info = st(s.status);
        const title = `${s.code}: ${s.artisanName || info.label()}${s.categoryName ? ' – ' + s.categoryName : ''}`;
        return `<button type="button" class="stall-cell ${info.cell}${s.hasPendingReallocation ? ' has-request' : ''}" data-code="${esc(s.code)}" title="${esc(title)}" aria-label="${esc(title)}">
          <span class="stall-icon"><i class="fa-solid ${info.icon}"></i></span>
          <span class="stall-id">${esc(s.code)}</span>
          <span class="stall-name">${esc(s.artisanName ? s.artisanName.split(' ')[0] : info.label())}</span>
        </button>`;
      }).join('')}</div>`).join('');
  }

  function renderList(){
    const body = $('stallsBody');
    $('tabCountList').textContent = stalls.length;
    if (!stalls.length){ body.innerHTML = `<tr><td colspan="8" class="empty-state">${esc(t('NoStallsFound'))}</td></tr>`; return; }
    body.innerHTML = stalls.map(s => {
      const info = st(s.status);
      const location = [t('BlockLabel', s.block), s.rowLabel].filter(Boolean).join(', ');
      return `<tr>
        <td><strong>${esc(s.code)}</strong>${s.hasPendingReallocation ? ` <span class="badge badge-warning" style="font-size:.6rem">${esc(t('PendingReallocationBadge'))}</span>` : ''}</td>
        <td>${esc(s.name)}</td>
        <td>${esc(location)}</td>
        <td>${s.artisanName ? `${esc(s.artisanName)} <span style="font-size:.68rem;color:var(--text-muted)">${esc(s.artisanCode)}</span>` : '—'}</td>
        <td>${esc(s.categoryName || '—')}</td>
        <td>${esc(s.allottedDate ? formatDate(s.allottedDate) : '—')}</td>
        <td><span class="badge ${info.badge}">${esc(info.label())}</span></td>
        <td style="white-space:nowrap">
          <button type="button" class="btn btn-ghost btn-sm btn-icon" data-act="view" data-code="${esc(s.code)}" title="${esc(t('View'))}" aria-label="${esc(t('View'))}"><i class="fa-solid fa-eye"></i></button>
          <button type="button" class="btn btn-ghost btn-sm btn-icon" data-act="edit" data-code="${esc(s.code)}" title="${esc(t('Edit'))}" aria-label="${esc(t('Edit'))}"><i class="fa-solid fa-pen"></i></button>
          ${s.status === 'OCCUPIED' ? '' : `<button type="button" class="btn btn-ghost btn-sm btn-icon" data-act="delete" data-code="${esc(s.code)}" title="${esc(t('Delete'))}" aria-label="${esc(t('Delete'))}" style="color:var(--danger)"><i class="fa-solid fa-trash"></i></button>`}
        </td></tr>`;
    }).join('');
  }

  function renderReallocations(){
    const body = $('reallocBody');
    if (!reallocations.length){ body.innerHTML = `<tr><td colspan="8" class="empty-state">${esc(t('NoReallocations'))}</td></tr>`; return; }
    body.innerHTML = reallocations.map(r => {
      const [labelKey, badge] = REQ_STATUS[r.status] || REQ_STATUS.PENDING;
      const decision = r.status === 'PENDING' ? `
          <button type="button" class="btn btn-primary btn-sm" data-act="approve" data-id="${esc(r.id)}"><i class="fa-solid fa-check"></i> ${esc(t('Approve'))}</button>
          <button type="button" class="btn btn-outline btn-sm" data-act="reject" data-id="${esc(r.id)}">${esc(t('Reject'))}</button>`
        : `<span style="font-size:.7rem;color:var(--text-muted)">${esc(r.decidedBy || '')}${r.decisionRemarks ? ' – ' + esc(r.decisionRemarks) : ''}</span>`;
      return `<tr>
        <td><code style="font-size:.7rem">${esc(r.id)}</code></td>
        <td><strong>${esc(r.stallCode)}</strong></td>
        <td>${esc(r.currentArtisanName || '—')}</td>
        <td>${esc(r.newArtisanName || t('VacateStallOption'))}</td>
        <td>${esc(t(REASON[r.reason] || 'ReasonOther'))}${r.remarks ? `<div style="font-size:.68rem;color:var(--text-muted)">${esc(r.remarks)}</div>` : ''}</td>
        <td>${esc(formatDate(r.requestedDate))}</td>
        <td><span class="badge ${badge}">${esc(t(labelKey))}</span></td>
        <td style="white-space:nowrap">${decision}</td></tr>`;
    }).join('');
  }

  // ── Errors ────────────────────────────────────────────────────
  function clearErrors(form){
    form.querySelectorAll('.form-group.has-error').forEach(g => g.classList.remove('has-error'));
    form.querySelectorAll('.form-error').forEach(e => { e.textContent = ''; });
  }
  function showErrors(prefix, errors){
    let first = null, n = 0;
    Object.entries(errors).forEach(([k, msg]) => {
      const el = $(prefix + k);
      if (!el) return;
      n++;
      el.textContent = msg;
      const group = el.closest('.form-group');
      group?.classList.add('has-error');
      if (!first) first = group?.querySelector('input,select,textarea');
    });
    first?.focus();
    return n;
  }

  // ── Stall details modal ───────────────────────────────────────
  function findStall(code){ return stalls.find(s => s.code === code) || null; }
  async function stallDetails(code){
    // the filtered list may not contain it (map click): fetch unfiltered
    let s = findStall(code);
    if (!s){
      const res = await fetch('/Stalls/Data?q=' + encodeURIComponent(code));
      if (res.ok) s = ((await res.json()).stalls || []).find(x => x.code === code) || null;
    }
    return s;
  }

  async function view(code){
    const s = await stallDetails(code);
    if (!s){ toast(t('Error'), t('StallNotFound'), 'error'); return; }
    const info = st(s.status);
    $('stallViewTitle').textContent = `${s.code} – ${s.name}`;
    const field = (label, value) => `<div class="profile-field"><div class="profile-field-label">${esc(label)}</div><div class="profile-field-value">${value}</div></div>`;
    $('stallViewBody').innerHTML = `<div class="detail-grid">
      ${field(t('Status'), `<span class="badge ${info.badge}">${esc(info.label())}</span>${s.hasPendingReallocation ? ` <span class="badge badge-warning">${esc(t('PendingReallocationBadge'))}</span>` : ''}`)}
      ${field(t('Location'), esc([t('BlockLabel', s.block), s.rowLabel].filter(Boolean).join(', ')) + (s.location ? `<div style="font-size:.72rem;color:var(--text-muted)">${esc(s.location)}</div>` : ''))}
      ${field(t('Artisan'), s.artisanName ? `${esc(s.artisanName)} <span style="font-size:.7rem;color:var(--text-muted)">${esc(s.artisanCode)}</span>` : '—')}
      ${field(t('Category'), esc(s.categoryName || '—'))}
      ${field(t('AllottedOn'), esc(s.allottedDate ? formatDate(s.allottedDate) : '—'))}
      ${field(t('AgreementUntil'), esc(s.agreementEndDate ? formatDate(s.agreementEndDate) : '—'))}
      ${field(t('MonthlyRent'), esc(money(s.monthlyRent)))}
      ${field(t('ProductsAtStall'), esc(s.productCount))}
    </div>${s.remarks ? field(t('Remarks'), esc(s.remarks)) : ''}`;

    const btn = (act, cls, icon, label) => `<button type="button" class="btn ${cls} btn-sm" data-act="${act}" data-code="${esc(s.code)}"><i class="fa-solid ${icon}"></i> ${esc(label)}</button>`;
    const actions = [btn('edit', 'btn-outline', 'fa-pen', t('Edit'))];
    if (s.status === 'VACANT') actions.push(btn('allocate', 'btn-primary', 'fa-user-plus', t('AllocateStall')));
    if (s.status === 'OCCUPIED') {
      if (!s.hasPendingReallocation) actions.push(btn('reallocate', 'btn-outline', 'fa-rotate', t('RequestReallocation')));
      actions.push(btn('vacate', 'btn-outline', 'fa-door-open', t('VacateStall')));
    } else {
      actions.push(btn('delete', 'btn-danger', 'fa-trash', t('Delete')));
    }
    $('stallViewActions').innerHTML = actions.join('');
    openModal('stallViewModal');
  }

  // ── Create / edit modal ───────────────────────────────────────
  function openForm(s){
    const form = $('stallForm');
    form.reset();
    clearErrors(form);
    $('stEditing').value = s ? s.code : '';
    $('stCode').value = s ? s.code : '';
    $('stCode').disabled = !!s;   // the code is fixed once created
    $('stName').value = s ? s.name : '';
    $('stBlock').value = s ? s.block : ($('filterBlock').value || '');
    $('stRow').value = s?.rowLabel || '';
    $('stCategory').value = s?.categoryId || '';
    $('stRent').value = s?.monthlyRent ?? '';
    $('stLocation').value = s?.location || '';
    $('stRemarks').value = s?.remarks || '';
    const occupied = s?.status === 'OCCUPIED';
    $('stStatus').value = s ? s.status : 'VACANT';
    $('stStatus').disabled = occupied;
    $('stStatusHint').style.display = occupied ? '' : 'none';
    $('stallModalTitle').textContent = s ? t('EditStall') : t('CreateNewStall');
    $('stallSaveText').textContent = s ? t('SaveChanges') : t('Create');
    formDirty = false;
    openModal('stallModal');
    (s ? $('stName') : $('stCode')).focus();
  }
  function closeForm(force){
    if (!force && formDirty && !confirm(t('DiscardChanges'))) return;
    formDirty = false;
    closeModal('stallModal');
  }

  function validateStall(v, editing){
    const e = {};
    if (!editing && !/^[A-Za-z0-9-]{1,20}$/.test(v.stallCode)) e.stall_code = t('ValStallCode');
    if (v.name.length < 2 || v.name.length > 100) e.name = t('ValStallName');
    if (!/^[A-Za-z0-9 -]{1,20}$/.test(v.block)) e.block = t('ValBlock');
    if (v.monthlyRent != null && (!Number.isFinite(v.monthlyRent) || v.monthlyRent < 0)) e.monthly_rent = t('ValRent');
    if ((v.remarks || '').length > 500) e.remarks = t('ValRemarks');
    return e;
  }

  async function saveStall(){
    if (saving) return;
    const form = $('stallForm');
    clearErrors(form);
    const editing = $('stEditing').value;
    const rent = $('stRent').value;
    const v = {
      stallCode: $('stCode').value.trim().toUpperCase(),
      name: $('stName').value.trim(),
      block: $('stBlock').value.trim().toUpperCase(),
      rowLabel: $('stRow').value.trim() || null,
      location: $('stLocation').value.trim() || null,
      status: $('stStatus').disabled ? null : $('stStatus').value,
      categoryId: $('stCategory').value || null,
      monthlyRent: rent === '' ? null : Number(rent),
      remarks: $('stRemarks').value.trim() || null
    };
    const errors = validateStall(v, !!editing);
    if (Object.keys(errors).length){ showErrors('st_err_', errors); return; }

    saving = true;
    $('stallSaveBtn').disabled = true;
    try {
      const { res, json } = await post(editing ? '/Stalls/Update/' + encodeURIComponent(editing) : '/Stalls/Create', v);
      if (failed(res, json, 'st_err_')) return;
      toast(t('Success'), t(editing ? 'StallUpdated' : 'StallCreated', json.code), 'success');
      closeForm(true);
      await load();
    } catch (e) {
      console.error(e);
      toast(t('Error'), t('CouldNotReachServer'), 'error');
    } finally {
      saving = false;
      $('stallSaveBtn').disabled = false;
    }
  }

  // ── Single-stall actions ──────────────────────────────────────
  async function remove(code){
    if (!confirm(t('ConfirmDeleteStall', code))) return;
    const { res, json } = await post('/Stalls/Delete/' + encodeURIComponent(code));
    if (failed(res, json)) return;
    closeModal('stallViewModal');
    toast(t('Success'), t('StallDeleted', code), 'success');
    load();
  }

  async function vacate(code){
    if (!confirm(t('ConfirmVacateStall', code))) return;
    const { res, json } = await post('/Stalls/Vacate/' + encodeURIComponent(code));
    if (failed(res, json)) return;
    closeModal('stallViewModal');
    toast(t('Success'), t('StallVacated', code), 'success');
    load();
  }

  function goToTab(tab){ document.querySelector(`#stallTabs .tab-item[data-tab="${tab}"]`)?.click(); }

  // ── Allocation ────────────────────────────────────────────────
  function resetAllocate(){
    const form = $('allocateForm');
    clearErrors(form);
    ['alStall', 'alArtisan', 'alCategory', 'alRent', 'alEnd', 'alRemarks'].forEach(id => { $(id).value = ''; });
    $('alDate').value = todayIso();
  }
  async function allocate(){
    if (saving) return;
    const form = $('allocateForm');
    clearErrors(form);
    const rent = $('alRent').value;
    const v = {
      stallCode: $('alStall').value, artisanCode: $('alArtisan').value, categoryId: $('alCategory').value || null,
      allottedDate: $('alDate').value, agreementEndDate: $('alEnd').value || null,
      monthlyRent: rent === '' ? null : Number(rent), remarks: $('alRemarks').value.trim() || null
    };
    const e = {};
    if (!v.stallCode) e.stall_code = t('ValSelectStall');
    if (!v.artisanCode) e.artisan_code = t('ValSelectArtisan');
    if (!v.allottedDate) e.allotted_date = t('ValDate');
    if (v.agreementEndDate && v.allottedDate && v.agreementEndDate <= v.allottedDate) e.agreement_end_date = t('ValAgreementEnd');
    if (v.monthlyRent != null && v.monthlyRent < 0) e.monthly_rent = t('ValRent');
    if (Object.keys(e).length){ showErrors('al_err_', e); return; }

    saving = true;
    $('allocateBtn').disabled = true;
    try {
      const { res, json } = await post('/Stalls/Allocate', v);
      if (failed(res, json, 'al_err_')) return;
      const artisan = freeArtisans.find(a => a.code === v.artisanCode);
      toast(t('Success'), t('StallAllocated', json.code, artisan ? artisan.name : json.artisanCode), 'success');
      resetAllocate();
      await load();
    } catch (err) {
      console.error(err);
      toast(t('Error'), t('CouldNotReachServer'), 'error');
    } finally {
      saving = false;
      $('allocateBtn').disabled = false;
    }
  }

  // ── Reallocation ──────────────────────────────────────────────
  async function requestReallocation(){
    if (saving) return;
    const form = $('reallocForm');
    clearErrors(form);
    const v = {
      stallCode: $('reStall').value, reason: $('reReason').value, newArtisanCode: $('reArtisan').value || null,
      requestedDate: $('reDate').value, remarks: $('reRemarks').value.trim() || null
    };
    const e = {};
    if (!v.stallCode) e.stall_code = t('ValSelectStall');
    if (!v.reason) e.reason = t('ValReason');
    if (!v.requestedDate) e.requested_date = t('ValDate');
    else if (v.requestedDate > todayIso()) e.requested_date = t('ValDateFuture');
    if (Object.keys(e).length){ showErrors('re_err_', e); return; }

    saving = true;
    $('reallocBtn').disabled = true;
    try {
      const { res, json } = await post('/Stalls/RequestReallocation', v);
      if (failed(res, json, 're_err_')) return;
      toast(t('Success'), t('ReallocationRequested', json.id), 'success');
      ['reStall', 'reReason', 'reArtisan', 'reRemarks'].forEach(id => { $(id).value = ''; });
      $('reDate').value = todayIso();
      await load();
    } catch (err) {
      console.error(err);
      toast(t('Error'), t('CouldNotReachServer'), 'error');
    } finally {
      saving = false;
      $('reallocBtn').disabled = false;
    }
  }

  async function decide(id, approve){
    const r = reallocations.find(x => x.id === id);
    if (approve && r && !confirm(t('ConfirmApproveReallocation', r.stallCode, r.newArtisanName || t('VacateStallOption')))) return;
    const remarks = prompt(t('DecisionRemarksPrompt'), '');
    if (remarks === null) return;
    const { res, json } = await post(`/Stalls/${approve ? 'ApproveReallocation' : 'RejectReallocation'}/${encodeURIComponent(id)}`, { remarks: remarks.trim() || null });
    if (failed(res, json)) return;
    toast(t('Success'), t(approve ? 'ReallocationApproved' : 'ReallocationRejected', id), 'success');
    load();
  }

  // ── Wiring ────────────────────────────────────────────────────
  function debounce(fn, ms){ let h; return (...a) => { clearTimeout(h); h = setTimeout(() => fn(...a), ms); }; }

  async function onAction(e){
    const btn = e.target.closest('[data-act]');
    if (!btn) return;
    const code = btn.dataset.code;
    switch (btn.dataset.act){
      case 'view': view(code); break;
      case 'edit': {
        closeModal('stallViewModal');
        const s = await stallDetails(code);
        if (s) openForm(s); else toast(t('Error'), t('StallNotFound'), 'error');
        break;
      }
      case 'delete': remove(code); break;
      case 'vacate': vacate(code); break;
      case 'allocate':
        closeModal('stallViewModal'); goToTab('allocate'); $('alStall').value = code; $('alArtisan').focus(); break;
      case 'reallocate':
        closeModal('stallViewModal'); goToTab('reallocate'); $('reStall').value = code; $('reReason').focus(); break;
      case 'approve': decide(btn.dataset.id, true); break;
      case 'reject': decide(btn.dataset.id, false); break;
    }
  }

  document.addEventListener('DOMContentLoaded', () => {
    $('alDate').value = todayIso();
    $('reDate').value = todayIso();
    $('reDate').max = todayIso();

    $('stallSearch').addEventListener('input', debounce(load, 250));
    ['filterStatus', 'filterCategory', 'filterBlock'].forEach(id => $(id).addEventListener('change', load));
    $('filterResetBtn').addEventListener('click', () => {
      $('stallSearch').value = ''; $('filterStatus').value = ''; $('filterCategory').value = ''; $('filterBlock').value = '';
      load();
    });
    $('mapBlock').addEventListener('change', renderMap);
    $('stallMap').addEventListener('click', e => { const cell = e.target.closest('.stall-cell'); if (cell) view(cell.dataset.code); });

    $('stallsBody').addEventListener('click', onAction);
    $('stallViewActions').addEventListener('click', onAction);
    $('reallocBody').addEventListener('click', onAction);

    $('createStallBtn').addEventListener('click', () => openForm(null));
    $('stallSaveBtn').addEventListener('click', saveStall);
    $('stallForm').addEventListener('submit', e => { e.preventDefault(); saveStall(); });
    $('stallForm').addEventListener('input', () => { formDirty = true; });
    $('stallCancelBtn').addEventListener('click', () => closeForm(false));
    $('stallModalClose').addEventListener('click', () => closeForm(false));

    $('allocateForm').addEventListener('submit', e => { e.preventDefault(); allocate(); });
    $('allocateResetBtn').addEventListener('click', resetAllocate);
    // the category follows the chosen artisan's craft unless picked explicitly
    $('alArtisan').addEventListener('change', () => {
      const a = freeArtisans.find(x => x.code === $('alArtisan').value);
      if (a && !$('alCategory').value) $('alCategory').value = a.categoryId;
    });
    $('reallocForm').addEventListener('submit', e => { e.preventDefault(); requestReallocation(); });

    $('stallsExportBtn').addEventListener('click', () => { window.location.href = '/Stalls/Export?' + qs(filters()); });

    load();
  });
})();
