// Artisan Applications: tabs per status (Approved / Under Review / Pending-New / Rejected) with their own search
// and export, new application / edit / delete, Process (take up for review) and Approve / Reject through the
// /Applications endpoints. Localized strings come from window.ApplicationsI18n (Views/Applications/Index.cshtml).
(function ApplicationsModule(){
  const TABS = {
    APPROVED: { body: 'approvedBody', search: 'searchApproved', count: 'countApproved', tab: 'approved', cols: 9 },
    UNDER_REVIEW: { body: 'reviewBody', search: 'searchReview', count: 'countReview', tab: 'review', cols: 8 },
    PENDING: { body: 'pendingBody', search: 'searchPending', count: 'countPending', tab: 'pending', cols: 8 },
    REJECTED: { body: 'rejectedBody', search: 'searchRejected', count: 'countRejected', tab: 'rejected', cols: 8 }
  };
  const STATUS = {
    PENDING: ['PendingLabel', 'badge-danger'],
    UNDER_REVIEW: ['UnderReviewLabel', 'badge-warning'],
    APPROVED: ['ApprovedLabel', 'badge-success'],
    REJECTED: ['RejectedLabel', 'badge-neutral']
  };
  let categories = [];
  let vacantStalls = [];
  let selectsReady = false;
  let loadSeq = 0;
  const tabSeq = {};
  let saving = false;
  let formDirty = false;
  let current = null;   // application shown in the review modal

  // ── i18n / formatting ─────────────────────────────────────────
  const I18N = window.ApplicationsI18n || {};
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
  function formatDate(iso){
    const m = /^(\d{4})-(\d{2})-(\d{2})$/.exec(iso || '');
    if (!m) return iso || '—';
    const d = new Date(+m[1], +m[2] - 1, +m[3]);
    return `${m[3]} ${d.toLocaleString(lang(), { month: 'short' })} ${m[1]}`;
  }
  const pad = n => String(n).padStart(2, '0');
  const iso = d => `${d.getFullYear()}-${pad(d.getMonth()+1)}-${pad(d.getDate())}`;
  const todayIso = () => iso(new Date());
  function yearsAgoIso(n){ const d = new Date(); d.setFullYear(d.getFullYear() - n); return iso(d); }
  const token = () => document.querySelector('#applicationForm input[name="__RequestVerificationToken"]')?.value || '';
  const toast = (title, msg, type) => { if (typeof showToast === 'function') showToast(esc(title), esc(msg), type); };
  const qs = obj => Object.entries(obj).filter(([, v]) => v !== '' && v != null).map(([k, v]) => `${k}=${encodeURIComponent(v)}`).join('&');
  const statusBadge = s => `<span class="badge ${(STATUS[s] || STATUS.PENDING)[1]}">${esc(t((STATUS[s] || STATUS.PENDING)[0]))}</span>`;
  const shared = () => ({ categoryId: $('appFilterCategory').value, district: $('appFilterDistrict').value });

  async function post(url, body){
    const res = await fetch(url, {
      method: 'POST',
      headers: Object.assign({ 'RequestVerificationToken': token() }, body ? { 'Content-Type': 'application/json' } : {}),
      body: body ? JSON.stringify(body) : undefined
    });
    const json = await res.json().catch(() => ({}));
    return { res, json };
  }
  const firstError = json => Object.values(json.errors || {})[0];

  // ── Load + render ─────────────────────────────────────────────
  async function load(){
    const seq = ++loadSeq;
    try {
      const res = await fetch('/Applications/Data?' + qs(shared()));
      if (!res.ok){ console.warn('Applications: data request failed', res.status); return; }
      const json = await res.json();
      if (seq !== loadSeq) return;
      categories = json.categories || [];
      vacantStalls = json.vacantStalls || [];
      renderStats(json.stats);
      renderSelects(json.districts || []);
      Object.keys(TABS).forEach(status => {
        // a tab with its own search text asks the server for its matches
        if ($(TABS[status].search).value.trim()) loadTab(status);
        else renderTab(status, (json.applications || []).filter(a => a.status === status));
      });
    } catch (e) {
      console.error(e);
      toast(t('Error'), t('CouldNotReachServer'), 'error');
    }
  }

  async function loadTab(status){
    const seq = tabSeq[status] = (tabSeq[status] || 0) + 1;
    try {
      const res = await fetch('/Applications/Data?' + qs(Object.assign({ status, q: $(TABS[status].search).value.trim() }, shared())));
      if (!res.ok) return;
      const json = await res.json();
      if (seq !== tabSeq[status]) return;
      renderTab(status, json.applications || []);
    } catch (e) {
      console.error(e);
      toast(t('Error'), t('CouldNotReachServer'), 'error');
    }
  }

  function renderStats(s){
    const set = (id, v) => { const el = $(id); if (el) el.textContent = v; };
    set('statTotal', s.total); set('statApproved', s.approved); set('statReview', s.underReview); set('statPending', s.pending);
    set('countApproved', s.approved); set('countReview', s.underReview); set('countPending', s.pending); set('countRejected', s.rejected);
  }

  function renderSelects(districts){
    if (selectsReady) return;
    const opts = list => list.map(o => `<option value="${esc(o.value)}">${esc(o.label)}</option>`).join('');
    const cat = categories.map(c => ({ value: c.id, label: c.name }));
    const dist = districts.map(d => ({ value: d, label: d }));
    $('appFilterCategory').insertAdjacentHTML('beforeend', opts(cat));
    $('apCategory').insertAdjacentHTML('beforeend', opts(cat));
    $('appFilterDistrict').insertAdjacentHTML('beforeend', opts(dist));
    $('apDistrict').insertAdjacentHTML('beforeend', opts(dist));
    selectsReady = true;
  }

  const idCell = a => `<td><code style="font-size:.7rem;color:var(--primary)">${esc(a.id)}</code></td>`;
  const nameCells = a => `<td><strong>${esc(a.fullName)}</strong></td><td>${esc(a.categoryName || '—')}</td><td>${esc(a.district)}</td>`;
  const iconBtn = (act, id, icon, label, style) =>
    `<button type="button" class="btn btn-ghost btn-sm btn-icon" data-act="${act}" data-id="${esc(id)}" title="${esc(label)}" aria-label="${esc(label)}"${style ? ` style="${style}"` : ''}><i class="fa-solid ${icon}"></i></button>`;
  const editDelete = a => iconBtn('edit', a.id, 'fa-pen', t('Edit')) + iconBtn('delete', a.id, 'fa-trash', t('Delete'), 'color:var(--danger)');

  const ROW = {
    APPROVED: a => `<tr>${idCell(a)}${nameCells(a)}
      <td>${a.artisanCode ? `<code style="font-size:.7rem">${esc(a.artisanCode)}</code>` : '—'}</td>
      <td>${a.stallCode ? `<span class="badge badge-primary">${esc(a.stallCode)}</span>` : '—'}</td>
      <td>${esc(formatDate(a.decidedDate))}</td><td>${esc(a.decidedBy || '—')}</td>
      <td style="white-space:nowrap">${iconBtn('view', a.id, 'fa-eye', t('View'))}</td></tr>`,
    UNDER_REVIEW: a => `<tr>${idCell(a)}${nameCells(a)}
      <td>${esc(formatDate(a.submittedDate))}</td>
      <td><span style="color:var(--warning);font-weight:600">${esc(t('DaysCount', a.daysWaiting))}</span></td>
      <td>${esc(a.reviewer || '—')}</td>
      <td style="white-space:nowrap"><button type="button" class="btn btn-primary btn-sm" data-act="review" data-id="${esc(a.id)}"><i class="fa-solid fa-check"></i> ${esc(t('Review'))}</button> ${editDelete(a)}</td></tr>`,
    PENDING: a => `<tr>${idCell(a)}${nameCells(a)}
      <td>${esc(a.mobile)}</td><td>${esc(formatDate(a.submittedDate))}</td>
      <td><span style="color:var(--danger);font-weight:600">${esc(t('DaysCount', a.daysWaiting))}</span></td>
      <td style="white-space:nowrap"><button type="button" class="btn btn-accent btn-sm" data-act="process" data-id="${esc(a.id)}"><i class="fa-solid fa-magnifying-glass"></i> ${esc(t('Process'))}</button> ${editDelete(a)}</td></tr>`,
    REJECTED: a => `<tr>${idCell(a)}${nameCells(a)}
      <td>${esc(formatDate(a.decidedDate))}</td><td>${esc(a.decidedBy || '—')}</td>
      <td style="max-width:240px;white-space:normal">${esc(a.decisionRemarks || '—')}</td>
      <td style="white-space:nowrap">${iconBtn('view', a.id, 'fa-eye', t('View'))}${iconBtn('delete', a.id, 'fa-trash', t('Delete'), 'color:var(--danger)')}</td></tr>`
  };

  function renderTab(status, rows){
    const tab = TABS[status];
    const body = $(tab.body);
    body.innerHTML = rows.length ? rows.map(ROW[status]).join('')
      : `<tr><td colspan="${tab.cols}" class="empty-state">${esc(t('NoApplicationsFound'))}</td></tr>`;
  }

  // ── Review / details ──────────────────────────────────────────
  async function fetchApplication(id){
    const res = await fetch('/Applications/Get/' + encodeURIComponent(id));
    if (res.status === 404){ toast(t('Error'), t('ApplicationNotFound'), 'error'); load(); return null; }
    if (!res.ok){ toast(t('Error'), t('CouldNotReachServer'), 'error'); return null; }
    return res.json();
  }

  function clearReviewErrors(){
    ['err_stall_code', 'err_rv_remarks', 'err_application'].forEach(id => { $(id).textContent = ''; });
    document.querySelectorAll('#rv-decide .form-group.has-error').forEach(g => g.classList.remove('has-error'));
  }
  function showReviewErrors(errors){
    const map = { stall_code: 'err_stall_code', remarks: 'err_rv_remarks', application: 'err_application' };
    let n = 0;
    Object.entries(errors || {}).forEach(([k, msg]) => {
      const el = $(map[k] || 'err_application');
      el.textContent = msg; el.closest('.form-group')?.classList.add('has-error'); n++;
    });
    return n;
  }

  async function openReview(id){
    const a = await fetchApplication(id);
    if (!a) return;
    current = a;
    const set = (el, v) => { $(el).textContent = v == null || v === '' ? '—' : v; };
    const open = a.status === 'PENDING' || a.status === 'UNDER_REVIEW';
    $('reviewModalTitle').textContent = open ? t('ApplicationReview') : t('ApplicationDetails');
    set('rv-id', a.id);
    $('rv-status').innerHTML = statusBadge(a.status);
    set('rv-name', a.fullName);
    set('rv-cat', a.categoryName);
    set('rv-district', a.district);
    set('rv-phone', a.mobile);
    set('rv-dob', formatDate(a.dateOfBirth));
    set('rv-aadhaar', a.aadhaarMasked);
    set('rv-years', t('YearsCount', a.yearsOfExperience));
    set('rv-submitted', formatDate(a.submittedDate) + (a.daysWaiting != null ? ` (${t('DaysCount', a.daysWaiting)})` : ''));
    set('rv-reviewer', a.reviewer);
    $('rv-decisionWrap').style.display = open ? 'none' : '';
    set('rv-decision', a.decidedDate ? `${formatDate(a.decidedDate)} · ${a.decidedBy || ''}` : '');
    set('rv-address', a.address);
    $('rv-skillsWrap').style.display = a.skills ? '' : 'none';
    set('rv-skills', a.skills);
    $('rv-decisionRemarksWrap').style.display = a.decisionRemarks ? '' : 'none';
    set('rv-decisionRemarks', a.decisionRemarks);

    // stall picker: stalls of the applicant's craft first
    const stalls = vacantStalls.slice().sort((x, y) => (y.categoryName === a.categoryName) - (x.categoryName === a.categoryName));
    $('rvStall').innerHTML = `<option value="">${esc(stalls.length ? t('NoStallNow') : t('NoVacantStalls'))}</option>` +
      stalls.map(s => `<option value="${esc(s.code)}">${esc(s.code)}${s.categoryName ? ' – ' + esc(s.categoryName) : ''}</option>`).join('');
    $('rvRemarks').value = '';
    clearReviewErrors();
    $('rv-decide').style.display = open ? '' : 'none';
    ['rvEditBtn', 'rvRejectBtn', 'rvApproveBtn'].forEach(b => { $(b).style.display = open ? '' : 'none'; });
    openModal('reviewModal');
  }

  async function decide(approve){
    if (!current || saving) return;
    clearReviewErrors();
    const remarks = $('rvRemarks').value.trim();
    if (!approve && !remarks){ showReviewErrors({ remarks: t('ValRejectRemarks') }); $('rvRemarks').focus(); return; }
    saving = true;
    ['rvRejectBtn', 'rvApproveBtn'].forEach(b => { $(b).disabled = true; });
    try {
      const id = current.id;
      const body = approve ? { stallCode: $('rvStall').value || null, remarks: remarks || null } : { remarks };
      const { res, json } = await post((approve ? '/Applications/Approve/' : '/Applications/Reject/') + encodeURIComponent(id), body);
      if (res.status === 404){ toast(t('Error'), t('ApplicationNotFound'), 'error'); closeModal('reviewModal'); load(); return; }
      if (!res.ok){ showReviewErrors(json.errors); toast(t('Error'), firstError(json) || `${t('CheckForm')} (${res.status})`, 'error'); return; }
      if (approve) toast(t('ApplicationApproved'), json.stallCode ? t('ApplicationApprovedWithStall', id, json.artisanCode, json.stallCode) : t('ApplicationApprovedMsg', id, json.artisanCode), 'success');
      else toast(t('ApplicationRejected'), t('ApplicationRejectedMsg', id), 'success');
      closeModal('reviewModal');
      await load();
    } catch (e) {
      console.error(e);
      toast(t('Error'), t('CouldNotReachServer'), 'error');
    } finally {
      saving = false;
      ['rvRejectBtn', 'rvApproveBtn'].forEach(b => { $(b).disabled = false; });
    }
  }

  async function processApp(id){
    try {
      const { res, json } = await post('/Applications/Process/' + encodeURIComponent(id));
      if (res.status === 404){ toast(t('Error'), t('ApplicationNotFound'), 'error'); load(); return; }
      if (!res.ok){ toast(t('Error'), firstError(json) || `${t('CheckForm')} (${res.status})`, 'error'); load(); return; }
      toast(t('Success'), t('ApplicationProcessed', id), 'success');
      await load();
      openReview(id);
    } catch (e) {
      console.error(e);
      toast(t('Error'), t('CouldNotReachServer'), 'error');
    }
  }

  async function remove(id){
    if (!confirm(t('ConfirmDeleteApplication', id))) return;
    try {
      const { res, json } = await post('/Applications/Delete/' + encodeURIComponent(id));
      if (res.status === 404){ toast(t('Error'), t('ApplicationNotFound'), 'error'); load(); return; }
      if (!res.ok){ toast(t('Error'), firstError(json) || `${t('CheckForm')} (${res.status})`, 'error'); return; }
      toast(t('Success'), t('ApplicationDeleted', id), 'success');
      load();
    } catch (e) {
      console.error(e);
      toast(t('Error'), t('CouldNotReachServer'), 'error');
    }
  }

  // ── New application / edit ────────────────────────────────────
  function clearErrors(){
    const form = $('applicationForm');
    form.querySelectorAll('.form-group.has-error').forEach(g => g.classList.remove('has-error'));
    form.querySelectorAll('.form-error').forEach(e => { e.textContent = ''; });
  }
  function showErrors(errors){
    let first = null, n = 0;
    Object.entries(errors).forEach(([k, msg]) => {
      const el = document.querySelector('#applicationForm #err_' + k);
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

  function openForm(a){
    $('applicationForm').reset();
    clearErrors();
    $('apEditing').value = a ? a.id : '';
    $('apName').value = a?.fullName || '';
    $('apDob').value = a?.dateOfBirth || '';
    $('apGender').value = a?.gender || 'Female';
    $('apMobile').value = a?.mobile || '';
    $('apEmail').value = a?.email || '';
    $('apAadhaar').value = '';
    $('apAadhaarReq').style.display = a ? 'none' : '';
    $('apAadhaarHint').textContent = a ? t('AadhaarKeepHint', a.aadhaarMasked) : t('AadhaarStoredHint');
    $('apAddress').value = a?.address || '';
    $('apDistrict').value = a?.district || '';
    $('apSubmitted').value = a?.submittedDate || todayIso();
    $('apCategory').value = a?.categoryId || '';
    $('apYears').value = a ? a.yearsOfExperience : 0;
    $('apSkills').value = a?.skills || '';
    $('apRemarks').value = a?.remarks || '';
    $('applicationModalText').textContent = a ? t('EditApplication') : t('NewArtisanApplication');
    $('applicationSaveText').textContent = a ? t('SaveChanges') : t('SubmitApplication');
    $('apDob').max = yearsAgoIso(18);
    $('apSubmitted').max = todayIso();
    formDirty = false;
    openModal('applicationModal');
    $('apName').focus();
  }
  function closeForm(force){
    if (!force && formDirty && !confirm(t('DiscardChanges'))) return;
    formDirty = false;
    closeModal('applicationModal');
  }

  function formValues(){
    return {
      fullName: $('apName').value.trim(),
      dateOfBirth: $('apDob').value,
      gender: $('apGender').value,
      mobile: $('apMobile').value.trim(),
      email: $('apEmail').value.trim() || null,
      address: $('apAddress').value.trim(),
      aadhaar: $('apAadhaar').value.trim() || null,
      district: $('apDistrict').value,
      categoryId: $('apCategory').value,
      yearsOfExperience: Number($('apYears').value || 0),
      skills: $('apSkills').value.trim() || null,
      submittedDate: $('apSubmitted').value || null,
      remarks: $('apRemarks').value.trim() || null
    };
  }

  // mirrors the server rules; the server has the final say (duplicates, Aadhaar checksum)
  function validate(v, editing){
    const e = {};
    if (v.fullName.length < 2 || v.fullName.length > 100) e.full_name = t('ValFullName');
    if (!v.dateOfBirth) e.date_of_birth = t('ValDobRequired');
    else if (v.dateOfBirth > todayIso()) e.date_of_birth = t('ValDobFuture');
    else if (v.dateOfBirth > yearsAgoIso(18)) e.date_of_birth = t('ValArtisanMinAge');
    const mobile = v.mobile.replace(/[\s-]/g, '').replace(/^\+91/, '').replace(/^0(?=\d{10}$)/, '');
    if (!/^[6-9]\d{9}$/.test(mobile)) e.mobile = t('ValArtisanMobile');
    if (v.email && !/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(v.email)) e.email = t('ValEmail');
    if (v.address.length < 5) e.address = t('ValAddress');
    if ((!editing || v.aadhaar) && !/^[2-9]\d{3}[\s-]?\d{4}[\s-]?\d{4}$/.test(v.aadhaar || '')) e.aadhaar = t('ValAadhaar');
    if (!v.district) e.district = t('ValDistrict');
    if (!v.categoryId) e.category_id = t('ValCraftCategory');
    if (!Number.isInteger(v.yearsOfExperience) || v.yearsOfExperience < 0 || v.yearsOfExperience > 80) e.years_of_experience = t('ValExperience');
    if (v.submittedDate && v.submittedDate > todayIso()) e.submitted_date = t('ValDateFuture');
    return e;
  }

  async function save(){
    if (saving) return;
    clearErrors();
    const editing = $('apEditing').value;
    const v = formValues();
    const errors = validate(v, !!editing);
    if (Object.keys(errors).length){ showErrors(errors); return; }

    saving = true;
    $('applicationSaveBtn').disabled = true;
    try {
      const { res, json } = await post(editing ? '/Applications/Update/' + encodeURIComponent(editing) : '/Applications/Create', v);
      if (res.status === 404){ toast(t('Error'), t('ApplicationNotFound'), 'error'); closeForm(true); load(); return; }
      if (!res.ok){
        const shown = showErrors(json.errors || {});
        toast(t('Error'), shown ? t('CheckForm') : (firstError(json) || `${t('CheckForm')} (${res.status})`), 'error');
        return;
      }
      toast(t('Success'), t(editing ? 'ApplicationUpdated' : 'ApplicationCreated', json.id), 'success');
      closeForm(true);
      await load();
    } catch (e) {
      console.error(e);
      toast(t('Error'), t('CouldNotReachServer'), 'error');
    } finally {
      saving = false;
      $('applicationSaveBtn').disabled = false;
    }
  }

  async function edit(id){
    const a = await fetchApplication(id);
    if (a) openForm(a);
  }

  // ── Wiring ────────────────────────────────────────────────────
  function debounce(fn, ms){ let h; return (...a) => { clearTimeout(h); h = setTimeout(() => fn(...a), ms); }; }

  function exportUrl(status){
    const q = status ? $(TABS[status].search).value.trim() : '';
    return '/Applications/Export?' + qs(Object.assign({ status: status || '', q }, shared()));
  }

  document.addEventListener('DOMContentLoaded', () => {
    // a link from the header search: /Applications/Index?tab=pending&q=APP-0001
    const params = new URLSearchParams(location.search);
    const linked = Object.keys(TABS).find(s => TABS[s].tab === params.get('tab'));
    if (linked){
      $(TABS[linked].search).value = params.get('q') || '';
      document.querySelector(`.tab-item[data-tab="${TABS[linked].tab}"]`)?.click();
    }

    Object.keys(TABS).forEach(status => {
      $(TABS[status].search).addEventListener('input', debounce(() => loadTab(status), 250));
    });
    ['appFilterCategory', 'appFilterDistrict'].forEach(id => $(id).addEventListener('change', load));
    $('appFilterResetBtn').addEventListener('click', () => {
      ['appFilterCategory', 'appFilterDistrict'].concat(Object.values(TABS).map(x => x.search)).forEach(id => { $(id).value = ''; });
      load();
    });

    document.querySelector('[data-tabs]').addEventListener('click', e => {
      const btn = e.target.closest('[data-act]');
      if (!btn) return;
      const id = btn.dataset.id;
      switch (btn.dataset.act){
        case 'view': case 'review': openReview(id); break;
        case 'process': processApp(id); break;
        case 'edit': edit(id); break;
        case 'delete': remove(id); break;
      }
    });
    document.querySelectorAll('[data-export-status]').forEach(b =>
      b.addEventListener('click', () => { window.location.href = exportUrl(b.dataset.exportStatus); }));
    $('appsExportAllBtn').addEventListener('click', () => { window.location.href = exportUrl(''); });

    $('rvApproveBtn').addEventListener('click', () => decide(true));
    $('rvRejectBtn').addEventListener('click', () => decide(false));
    $('rvEditBtn').addEventListener('click', () => { if (!current) return; closeModal('reviewModal'); edit(current.id); });

    $('newApplicationBtn').addEventListener('click', () => openForm(null));
    $('applicationSaveBtn').addEventListener('click', save);
    $('applicationForm').addEventListener('submit', e => { e.preventDefault(); save(); });
    $('applicationForm').addEventListener('input', () => { formDirty = true; });
    $('applicationCancelBtn').addEventListener('click', () => closeForm(false));
    $('applicationModalClose').addEventListener('click', () => closeForm(false));

    load();
  });
})();
