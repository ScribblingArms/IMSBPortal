// SHG Applications page: list and details of the stall applications Self-Help Groups make on the IMSB website
// (SHGDetails), through /ShgApplications/Data and /ShgApplications/Get; Allocate Stall and Reject for applications in process. The server only returns what the user may see
// (a District Officer: their own district). Localized strings come from window.ShgApplicationsI18n (Views/ShgApplications/Index.cshtml).
(function ShgApplicationsModule(){
  let rows = [];

  const I18N = window.ShgApplicationsI18n || {};
  function t(key, ...args){
    let s = I18N[key] || key;
    args.forEach((a, i) => { s = s.split('{' + i + '}').join(a); });
    return s;
  }
  function esc(v){
    return String(v == null ? '' : v).replace(/[&<>"']/g, c => ({ '&':'&amp;', '<':'&lt;', '>':'&gt;', '"':'&quot;', "'":'&#39;' }[c]));
  }
  const $ = id => document.getElementById(id);
  const toast = (title, msg, type) => { if (typeof showToast === 'function') showToast(esc(title), esc(msg), type); };
  const lang = () => document.documentElement.lang || undefined;

  function displayDate(iso){
    if (!iso) return '—';
    const d = new Date(iso);
    return isNaN(d) ? '—' : d.toLocaleDateString(lang(), { day: '2-digit', month: 'short', year: 'numeric' });
  }
  function displayMonth(ym){
    if (!ym) return '';
    const [y, m] = ym.split('-').map(Number);
    return new Date(y, m - 1, 1).toLocaleDateString(lang(), { month: 'long', year: 'numeric' });
  }
  const money = v => v == null ? '' : '₹' + Number(v).toLocaleString('en-IN', { maximumFractionDigits: 2 });

  // ── List ──────────────────────────────────────────────────────
  async function load(){
    try {
      const res = await fetch('/ShgApplications/Data', { headers: { 'Accept': 'application/json' } });
      if (!res.ok){ toast(t('Error'), t('CouldNotReachServer'), 'error'); return; }
      const json = await res.json();
      rows = json.rows || [];
      $('shgStatTotal').textContent = json.stats?.total ?? 0;
      $('shgStatInProcess').textContent = json.stats?.inProcess ?? 0;
      $('shgStatSubmitted').textContent = json.stats?.submitted ?? 0;
      $('shgStatIncomplete').textContent = json.stats?.incomplete ?? 0;
      $('shgStatRejected').textContent = json.stats?.rejected ?? 0;
      // only users who see every district get the district filter
      const select = $('shgFilterDistrict');
      if (json.scope?.allDistricts){
        select.length = 1;
        (json.districts || []).forEach(d => select.add(new Option(d.label, d.value)));
        select.hidden = false;
      }
      render();
    } catch (e) {
      console.error(e);
      toast(t('Error'), t('CouldNotReachServer'), 'error');
    }
  }

  // incomplete (not submitted on the website) / inprocess (submitted, no decision yet) / submitted (stall allotted) / rejected
  const STATUS_BADGE = { incomplete: 'badge-neutral', inprocess: 'badge-warning', submitted: 'badge-success', rejected: 'badge-danger' };
  function statusBadge(r){
    return `<span class="badge ${STATUS_BADGE[r.status] || 'badge-neutral'}">${esc(r.statusLabel)}</span>`;
  }

  function render(){
    const q = $('shgSearch').value.trim().toLowerCase();
    const status = $('shgFilterStatus').value;
    const district = $('shgFilterDistrict').value;
    const shown = rows.filter(r =>
      (!status || r.status === status)
      && (!district || String(r.districtId) === district)
      && (!q || [r.reference, r.shgName, r.shgId, r.mandal, r.village, r.repName, r.repMobile, r.districtLabel, r.category, r.rejectionReason]
                  .join(' ').toLowerCase().includes(q)));
    $('shgCount').textContent = shown.length;
    $('shgBody').innerHTML = shown.length ? shown.map(r => `<tr>
      <td style="white-space:nowrap"><strong>${esc(r.reference || '—')}</strong></td>
      <td><strong>${esc(r.shgName)}</strong><div style="font-size:12px;color:var(--text-muted)">${esc(r.shgId)}</div></td>
      <td>${esc(r.districtLabel || '—')}</td>
      <td>${esc(r.mandal || '—')}</td>
      <td>${esc(r.repName || '—')}<div style="font-size:12px;color:var(--text-muted)">${esc(r.repMobile || '')}</div></td>
      <td>${esc(r.category || '—')}</td>
      <td>${statusBadge(r)}${r.status === 'rejected' && r.rejectionReason
        ? `<div class="shg-reject-reason" title="${esc(r.rejectionReason)}"><span>${esc(t('ShgRejectionReason'))}:</span> ${esc(r.rejectionReason)}</div>` : ''}</td>
      <td style="white-space:nowrap">${r.stallCode ? `<span class="badge badge-primary">${esc(r.stallCode)}</span>` : '—'}</td>
      <td style="white-space:nowrap">${esc(displayDate(r.submittedAt || r.createdAt))}</td>
      <td style="white-space:nowrap"><button type="button" class="btn btn-ghost btn-sm btn-icon" data-act="view" data-id="${esc(r.id)}" title="${esc(t('View'))}" aria-label="${esc(t('View'))}"><i class="fa-solid fa-eye"></i></button>${
        // an application holding a stall cannot be deleted (vacate it first): the button is shown disabled with the reason
        r.stallCode
          ? `<button type="button" class="btn btn-ghost btn-sm btn-icon shg-delete" disabled aria-disabled="true" title="${esc(t('ShgValDeleteHasStall', r.stallCode))}" aria-label="${esc(t('Delete'))}"><i class="fa-solid fa-trash"></i></button>`
          : `<button type="button" class="btn btn-ghost btn-sm btn-icon shg-delete" data-act="delete" data-id="${esc(r.id)}" title="${esc(t('Delete'))}" aria-label="${esc(t('Delete'))}"><i class="fa-solid fa-trash"></i></button>`
      }</td>
    </tr>`).join('') : `<tr><td colspan="10" class="empty-state" style="text-align:center">${esc(t('ShgNoApplications'))}</td></tr>`;
  }

  // ── Details ───────────────────────────────────────────────────
  function field(label, value, wide){
    const empty = value == null || value === '';
    return `<div class="form-group"${wide ? ' style="grid-column:1/-1"' : ''}><div class="form-label">${esc(t(label))}</div>
      <div class="shg-value">${empty ? `<span class="shg-muted">${esc(t('ShgNotGiven'))}</span>` : esc(value)}</div></div>`;
  }
  function htmlField(label, html, wide){
    return `<div class="form-group"${wide ? ' style="grid-column:1/-1"' : ''}><div class="form-label">${esc(t(label))}</div><div class="shg-value">${html}</div></div>`;
  }
  function section(title, body){
    return `<div class="shg-section"><h3>${esc(t(title))}</h3>${body ?? `<div class="shg-muted">${esc(t('ShgStepNotSaved'))}</div>`}</div>`;
  }
  const grid = parts => `<div class="form-grid col-2">${parts.join('')}</div>`;
  const yesNo = v => v == null ? '' : t(v ? 'Yes' : 'No');
  // every uploaded file is shown the same way: a tile that opens the file (a photo shows itself, a PDF its icon)
  function fileTile(f, label){
    const open = `href="${esc(f.url)}" target="_blank" rel="noopener" title="${esc(t('ShgOpenFile'))}"`;
    return f.isPdf
      ? `<a ${open} class="shg-pdf" aria-label="${esc(label)} (PDF)"><i class="fa-solid fa-file-pdf"></i><span>PDF</span></a>`
      : `<a ${open}><img src="${esc(f.url)}" alt="${esc(label)}" loading="lazy"></a>`;
  }
  function fileTiles(files, label, emptyKey){
    return files.length
      ? `<div class="shg-photos">${files.map(f => fileTile(f, label)).join('')}</div>`
      : `<span class="shg-muted">${esc(t(emptyKey))}</span>`;
  }

  function detailsHtml(a){
    const s = a.shg, r = a.representative, p = a.product, st = a.stall, d = a.declaration;
    const head = grid([
      field('ShgReference', a.reference || t('ShgNotSubmittedYet')),
      htmlField('Status', statusBadge(a)),
      field('ShgCreatedOn', displayDate(a.createdAt)),
      field('ShgSubmittedOn', a.submittedAt ? displayDate(a.submittedAt) : '')
    ]);
    const shg = grid([
      field('ShgName', s.shgName), field('ShgIdLabel', s.shgId),
      field('ShgVillageOrganisation', s.villageOrganisation), field('ShgMandalSamakhya', s.mandalSamakhya),
      field('District', s.districtLabel), field('ShgMandal', s.mandal),
      field('ShgVillage', s.village), field('ShgYearFormed', s.yearFormed), field('ShgMembers', s.memberCount)
    ]);
    const rep = r && grid([
      field('ShgRepresentative', r.name), field('ShgRepRole', r.role), field('ShgRepAge', r.age), field('ShgGuardian', r.guardianName),
      field('Mobile', r.mobile ? '+91 ' + r.mobile : ''), field('ShgAltMobile', r.altMobile ? '+91 ' + r.altMobile : ''),
      field('Email', r.email), field('ShgPinCode', r.pinCode), field('ShgAddress', r.address, true)
    ]);
    const photos = p && fileTiles(p.photos, t('ShgPhotos'), 'ShgNoPhotos');
    const product = p && grid([
      field('ShgProductCategory', p.category), field('ShgProductType', p.type),
      field('ShgPriceRange', p.priceMin == null ? '' : `${money(p.priceMin)} – ${money(p.priceMax)}`), field('ShgCapacity', p.capacity),
      field('ShgProductDescription', p.description, true), htmlField('ShgPhotos', photos, true)
    ]);
    const stall = st && grid([
      field('ShgDuration', st.duration), field('ShgStartMonth', displayMonth(st.startMonth)),
      field('ShgFacilities', st.facilities.join(', ')), field('ShgPrevExhibition', yesNo(st.previousExhibition)),
      ...(st.previousExhibition ? [field('ShgPrevExhibitionDetails', st.previousExhibitionDetails, true)] : [])
    ]);
    const docs = d && grid([
      htmlField('ShgPassbookDoc', fileTiles(d.shgDocument ? [d.shgDocument] : [], t('ShgPassbookDoc'), 'ShgNotGiven')),
      htmlField('ShgIdProofDoc', fileTiles(d.idProof ? [d.idProof] : [], t('ShgIdProofDoc'), 'ShgNotGiven')),
      field('ShgDeclTrue', yesNo(d.detailsTrue)), field('ShgDeclConsent', yesNo(d.consent))
    ]);
    const st2 = a.allottedStall;
    const allotment = st2 ? `<div class="shg-section shg-allotment"><h3><i class="fa-solid fa-store"></i> ${esc(t('ShgSectionAllotment'))}</h3>${grid([
      field('Stall', `${st2.code} – ${st2.name}`), field('AllotmentDate', displayDate(st2.allottedDate)),
      field('AgreementEndDate', st2.agreementEndDate ? displayDate(st2.agreementEndDate) : ''),
      field('MonthlyRent', st2.monthlyRent == null ? '' : money(st2.monthlyRent)), field('Remarks', st2.remarks, true)
    ])}</div>` : '';
    const rj = a.rejection;
    const rejection = rj ? `<div class="shg-section shg-rejection"><h3><i class="fa-solid fa-circle-xmark"></i> ${esc(t('ShgSectionRejection'))}</h3>${grid([
      field('ShgRejectedOn', displayDate(rj.rejectedAt)), field('ShgRejectedBy', rj.rejectedBy),
      field('ShgRejectionReason', rj.reason, true)
    ])}</div>` : '';
    // the decision (allotted stall or rejection) comes first, above the reference, status and dates
    return allotment + rejection + head
      + section('ShgSectionShg', shg) + section('ShgSectionRep', rep) + section('ShgSectionProduct', product)
      + section('ShgSectionStall', stall) + section('ShgSectionDocs', docs);
  }

  let current = null;   // the application shown in the details modal

  async function view(id){
    try {
      const res = await fetch('/ShgApplications/Get/' + encodeURIComponent(id), { headers: { 'Accept': 'application/json' } });
      if (!res.ok){ toast(t('Error'), t('CouldNotReachServer'), 'error'); return; }
      current = await res.json();
      $('shgViewBody').innerHTML = detailsHtml(current);
      // Allocate Stall: shown only while In process.
      // Reject: enabled while In process or Incomplete; disabled once a stall is allotted; hidden once rejected.
      $('shgAllocateOpenBtn').hidden = !current.canAllocate;
      const rejectBtn = $('shgRejectOpenBtn');
      rejectBtn.hidden = current.status === 'rejected';
      rejectBtn.disabled = !current.canReject;
      rejectBtn.setAttribute('aria-disabled', String(!current.canReject));
      rejectBtn.title = !current.canReject && current.allottedStall ? t('ShgValHasStall', current.allottedStall.code) : '';
      openModal('shgViewModal');
    } catch (e) {
      console.error(e);
      toast(t('Error'), t('CouldNotReachServer'), 'error');
    }
  }

  // ── Allocate Stall (the stall goes to the SHG itself: no artisan, category from the application) ──
  const SA_FIELDS = ['request', 'stall_code', 'monthly_rent', 'allotted_date', 'agreement_end_date', 'remarks'];
  let allocating = false;
  const token = () => document.querySelector('#shgAllocateForm input[name="__RequestVerificationToken"]')?.value || '';
  const today = () => { const d = new Date(); d.setMinutes(d.getMinutes() - d.getTimezoneOffset()); return d.toISOString().slice(0, 10); };

  function clearAllocateErrors(){
    SA_FIELDS.forEach(k => { const el = $('sa_err_' + k); el.textContent = ''; el.closest('.form-group')?.classList.remove('has-error'); });
  }
  function showAllocateErrors(errors){
    Object.entries(errors).forEach(([k, msg]) => {
      const el = $('sa_err_' + k) || $('sa_err_request');
      el.textContent = msg;
      el.closest('.form-group')?.classList.add('has-error');
    });
  }
  function resetAllocateForm(){
    clearAllocateErrors();
    ['saStall', 'saRent', 'saEnd', 'saRemarks'].forEach(id => { $(id).value = ''; });
    $('saDate').value = today();
  }

  async function openAllocate(){
    if (!current || !current.canAllocate) return;
    $('saShgName').textContent = current.shg.shgName;
    $('saCategory').textContent = current.category || '—';
    resetAllocateForm();
    const select = $('saStall');
    select.innerHTML = `<option value="">${esc(t('SelectPlaceholder'))}</option>`;
    try {
      const res = await fetch('/ShgApplications/VacantStalls', { headers: { 'Accept': 'application/json' } });
      if (!res.ok){ toast(t('Error'), t('CouldNotReachServer'), 'error'); return; }
      const stalls = await res.json();
      if (!stalls.length) select.innerHTML = `<option value="">${esc(t('NoVacantStalls'))}</option>`;
      stalls.forEach(s => select.add(new Option(`${s.code} – ${s.name}`, s.code)));
      // the stall's own rent is offered as a starting point
      select.onchange = () => {
        const s = stalls.find(x => x.code === select.value);
        if (s && s.monthlyRent != null && !$('saRent').value) $('saRent').value = s.monthlyRent;
      };
    } catch (e) {
      console.error(e);
      toast(t('Error'), t('CouldNotReachServer'), 'error');
      return;
    }
    closeModal('shgViewModal');
    openModal('shgAllocateModal');
    select.focus();
  }

  // same rules as ShgApplicationsController.Allocate
  function validateAllocate(v){
    const e = {};
    if (!v.stallCode) e.stall_code = t('ValSelectStall');
    if (!v.allottedDate) e.allotted_date = t('ValDate');
    if (v.agreementEndDate && v.allottedDate && v.agreementEndDate <= v.allottedDate) e.agreement_end_date = t('ValAgreementEnd');
    if (v.monthlyRent != null && (isNaN(v.monthlyRent) || v.monthlyRent < 0 || v.monthlyRent > 10000000)) e.monthly_rent = t('ValRent');
    return e;
  }

  async function allocate(){
    if (allocating || !current) return;
    clearAllocateErrors();
    const rent = $('saRent').value.trim();
    const v = {
      stallCode: $('saStall').value,
      allottedDate: $('saDate').value,
      agreementEndDate: $('saEnd').value || null,
      monthlyRent: rent === '' ? null : Number(rent),
      remarks: $('saRemarks').value.trim() || null
    };
    const errors = validateAllocate(v);
    if (Object.keys(errors).length){ showAllocateErrors(errors); return; }

    allocating = true;
    $('saSaveBtn').disabled = true;
    try {
      const res = await fetch('/ShgApplications/Allocate/' + encodeURIComponent(current.id), {
        method: 'POST',
        headers: { 'Content-Type': 'application/json', 'Accept': 'application/json', 'RequestVerificationToken': token() },
        body: JSON.stringify(v)
      });
      const json = await res.json().catch(() => ({}));
      if (res.status === 400){ showAllocateErrors(json.errors || { request: t('CheckForm') }); return; }
      if (!res.ok){ toast(t('Error'), `${t('CheckForm')} (${res.status})`, 'error'); return; }
      toast(t('Success'), json.message || t('Success'), 'success');
      // done: close every popup and refresh the list (it now shows the stall code)
      closeModal('shgAllocateModal');
      closeModal('shgViewModal');
      current = null;
      await load();
    } catch (e) {
      console.error(e);
      toast(t('Error'), t('CouldNotReachServer'), 'error');
    } finally {
      allocating = false;
      $('saSaveBtn').disabled = false;
    }
  }

  // ── Reject (a reason is required; the application then shows as Rejected and gets no stall) ──
  let rejecting = false;
  const rejectToken = () => document.querySelector('#shgRejectForm input[name="__RequestVerificationToken"]')?.value || '';

  function clearRejectErrors(){
    ['request', 'reason'].forEach(k => { const el = $('sr_err_' + k); el.textContent = ''; el.closest('.form-group')?.classList.remove('has-error'); });
  }
  function showRejectErrors(errors){
    Object.entries(errors).forEach(([k, msg]) => {
      const el = $('sr_err_' + k) || $('sr_err_request');
      el.textContent = msg;
      el.closest('.form-group')?.classList.add('has-error');
    });
  }

  function openReject(){
    if (!current || !current.canReject) return;
    clearRejectErrors();
    $('srShgName').textContent = current.shg.shgName;
    $('srReference').textContent = current.reference || '—';
    $('srReason').value = '';
    closeModal('shgViewModal');
    openModal('shgRejectModal');
    $('srReason').focus();
  }

  async function reject(){
    if (rejecting || !current) return;
    clearRejectErrors();
    const reason = $('srReason').value.trim();
    // same rule as ShgApplicationsController.Reject
    if (!reason || reason.length > 500){ showRejectErrors({ reason: t('ShgValRejectReason') }); return; }

    rejecting = true;
    $('srSaveBtn').disabled = true;
    try {
      const res = await fetch('/ShgApplications/Reject/' + encodeURIComponent(current.id), {
        method: 'POST',
        headers: { 'Content-Type': 'application/json', 'Accept': 'application/json', 'RequestVerificationToken': rejectToken() },
        body: JSON.stringify({ reason })
      });
      const json = await res.json().catch(() => ({}));
      if (res.status === 400){ showRejectErrors(json.errors || { request: t('CheckForm') }); return; }
      if (!res.ok){ toast(t('Error'), `${t('CheckForm')} (${res.status})`, 'error'); return; }
      toast(t('Success'), json.message || t('Success'), 'success');
      closeModal('shgRejectModal');
      current = null;
      await load();
    } catch (e) {
      console.error(e);
      toast(t('Error'), t('CouldNotReachServer'), 'error');
    } finally {
      rejecting = false;
      $('srSaveBtn').disabled = false;
    }
  }

  // ── Delete (any status, but not while the SHG holds a stall) ──
  let deleting = false;
  async function remove(id){
    const r = rows.find(x => x.id === id);
    if (!r || r.stallCode || deleting) return;
    if (!confirm(t('ShgConfirmDelete', r.reference || '—', r.shgName))) return;
    deleting = true;
    try {
      const res = await fetch('/ShgApplications/Delete/' + encodeURIComponent(id), {
        method: 'POST',
        headers: { 'Accept': 'application/json', 'RequestVerificationToken': rejectToken() }
      });
      const json = await res.json().catch(() => ({}));
      if (res.status === 400){ toast(t('Error'), Object.values(json.errors || {})[0] || t('CheckForm'), 'error'); await load(); return; }
      if (!res.ok){ toast(t('Error'), `${t('CheckForm')} (${res.status})`, 'error'); return; }
      toast(t('Success'), json.message || t('Success'), 'success');
      await load();
    } catch (e) {
      console.error(e);
      toast(t('Error'), t('CouldNotReachServer'), 'error');
    } finally {
      deleting = false;
    }
  }

  document.addEventListener('DOMContentLoaded', () => {
    if (!$('shgBody')) return;
    $('shgSearch').addEventListener('input', render);
    $('shgFilterStatus').addEventListener('change', render);
    $('shgFilterDistrict').addEventListener('change', render);
    $('shgBody').addEventListener('click', e => {
      const btn = e.target.closest('[data-act]');
      if (!btn) return;
      if (btn.dataset.act === 'view') view(btn.dataset.id);
      else if (btn.dataset.act === 'delete') remove(btn.dataset.id);
    });
    $('shgAllocateOpenBtn').addEventListener('click', openAllocate);
    $('shgRejectOpenBtn').addEventListener('click', openReject);
    $('shgRejectForm').addEventListener('submit', e => { e.preventDefault(); reject(); });
    $('shgAllocateForm').addEventListener('submit', e => { e.preventDefault(); allocate(); });
    $('saResetBtn').addEventListener('click', resetAllocateForm);
    load();
  });
})();
