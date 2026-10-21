// Staff / HR page: list, filters, add / edit / delete through the /Staff endpoints.
// Localized strings come from window.StaffI18n (rendered by Views/Staff/Index.cshtml from SharedResource.*.resx).
(function StaffModule(){
  let depts = [];
  let staff = [];
  let shifts = [];
  let formDirty = false;
  let submitting = false;
  let loadSeq = 0;   // ignores out-of-order responses when filters change quickly

  // ── i18n ──────────────────────────────────────────────────────
  const I18N = window.StaffI18n || {};
  // t('PercentOfTotal', 40) -> "40% of total"; falls back to the key so missing strings are visible, not blank
  function t(key, ...args){
    let s = I18N[key] || key;
    args.forEach((a, i) => { s = s.split('{' + i + '}').join(a); });
    return s;
  }

  // escape values before putting them into innerHTML
  function esc(v){
    return String(v == null ? '' : v).replace(/[&<>"']/g, c => ({ '&':'&amp;', '<':'&lt;', '>':'&gt;', '"':'&quot;', "'":'&#39;' }[c]));
  }

  function statusLabel(status){
    const s = String(status || '').toUpperCase();
    if (s === 'ACTIVE') return t('Active');
    if (s === 'ON_LEAVE') return t('OnLeave');
    if (s === 'INACTIVE') return t('Inactive');
    return status || '';
  }

  function statusBadge(status){
    const s = String(status || '').toUpperCase();
    return s === 'ACTIVE' ? 'badge-success' : s === 'INACTIVE' ? 'badge-danger' : 'badge-warning';
  }

  // "2020-01-15" -> "15 Jan 2020" in the page language; parsed as a local date so it never shifts a day
  function formatDateDisplay(iso){
    if (!iso) return '—';
    const m = /^(\d{4})-(\d{2})-(\d{2})$/.exec(iso);
    if (!m) return iso;
    const d = new Date(+m[1], +m[2] - 1, +m[3]);
    const lang = document.documentElement.lang || undefined;
    return `${m[3]} ${d.toLocaleString(lang, { month: 'short' })} ${m[1]}`;
  }

  // ── Rendering ─────────────────────────────────────────────────
  function renderTable(){
    const staffBody = document.getElementById('staffBody');
    if (!staffBody) return;
    const rows = staff.map(s => `
      <tr>
        <td><code style="font-size:.7rem">${esc(s.id)}</code></td>
        <td><div style="display:flex;align-items:center;gap:8px"><div style="width:30px;height:30px;border-radius:50%;background:var(--primary-xlight);display:flex;align-items:center;justify-content:center;font-size:.65rem;font-weight:700;color:var(--primary)">${esc((s.full_name||'').split(' ').map(x=>x[0]||'').join('').slice(0,2))}</div><strong>${esc(s.full_name)}</strong></div></td>
        <td>${esc(s.department_name)}</td>
        <td>${esc(s.designation)}</td>
        <td>${esc(s.mobile)}</td>
        <td>${esc(formatDateDisplay(s.join_date))}</td>
        <td><span class="badge ${statusBadge(s.status)}">${esc(statusLabel(s.status))}</span></td>
        <td style="white-space:nowrap">
          <button type="button" class="btn btn-ghost btn-sm btn-icon" data-view-staff="${esc(s.id)}" title="${esc(t('View'))}"><i class="fa-solid fa-eye"></i></button>
          <button type="button" class="btn btn-ghost btn-sm btn-icon" data-edit-staff="${esc(s.id)}" title="${esc(t('Edit'))}"><i class="fa-solid fa-pen"></i></button>
          <button type="button" class="btn btn-ghost btn-sm btn-icon" data-delete-staff="${esc(s.id)}" title="${esc(t('Delete'))}" style="color:var(--danger)"><i class="fa-solid fa-trash"></i></button>
        </td>
      </tr>
    `).join('');
    staffBody.innerHTML = rows || `<tr><td colspan="8" style="text-align:center;color:var(--text-muted);padding:18px">${esc(t('NoStaffFound'))}</td></tr>`;
    staffBody.querySelectorAll('[data-edit-staff]').forEach(b => b.addEventListener('click', () => openEdit(b.dataset.editStaff)));
    staffBody.querySelectorAll('[data-delete-staff]').forEach(b => b.addEventListener('click', () => deleteStaff(b.dataset.deleteStaff)));
    staffBody.querySelectorAll('[data-view-staff]').forEach(b => b.addEventListener('click', () => {
      const s = staff.find(x => x.id === b.dataset.viewStaff);
      showToast(t('StaffTitle'), t('ViewingProfile', s ? s.full_name : ''), 'info');
    }));
  }

  function renderDeptCards(){
    const deptCards = document.getElementById('deptCards');
    if (!deptCards) return;
    const total = depts.reduce((sum, d) => sum + (d.count || 0), 0);
    deptCards.innerHTML = depts.map(d=>`
      <div class="card" style="border-top:3px solid ${esc(d.color)}">
        <div class="card-body" style="text-align:center;padding:18px 12px">
          <div style="font-size:1.6rem;font-weight:800;color:${esc(d.color)}">${d.count}</div>
          <div style="font-size:.78rem;font-weight:600;color:var(--text);margin-top:4px">${esc(d.name)}</div>
          <div style="font-size:.7rem;color:var(--text-muted);margin-top:2px">${esc(t('PercentOfTotal', total ? Math.round(d.count/total*100) : 0))}</div>
        </div>
      </div>`).join('');
  }

  function renderStats(stats){
    if (!stats) return;
    const set = (id, v) => { const el = document.getElementById(id); if (el) el.textContent = v; };
    set('statTotalStaff', stats.total);
    set('statActiveStaff', stats.active);
    set('statOnLeave', stats.onLeave);
    set('statDepartments', stats.departments);
    set('statContractExpiring', stats.contractExpiring);
  }

  function populateDeptAndShifts(){
    const dsel = document.getElementById('staffDepartment');
    const ssel = document.getElementById('staffShift');
    const fsel = document.getElementById('staffFilterDepartment');
    const deptOptions = depts.map(d => `<option value="${esc(d.id)}">${esc(d.name)}</option>`).join('');
    if (dsel){
      dsel.innerHTML = depts.length
        ? `<option value="">${esc(t('SelectPlaceholder'))}</option>` + deptOptions
        : `<option value="">${esc(t('NoDepartmentsConfigured'))}</option>`;
    }
    if (fsel){
      // keep the current filter selection when the list is rebuilt
      const current = fsel.value;
      fsel.innerHTML = `<option value="">${esc(t('AllDepartments'))}</option>` + deptOptions;
      fsel.value = current;
    }
    if (ssel){
      ssel.innerHTML = shifts.map(s => `<option value="${esc(s.id)}">${esc(s.name)}</option>`).join('');
    }
  }

  // ── Modal ─────────────────────────────────────────────────────
  function openModal(){
    const overlay = document.getElementById('addStaffModal');
    if (!overlay) return;
    // .open is what makes the overlay visible and clickable (style.css); display alone leaves it invisible
    overlay.style.display = 'flex';
    overlay.classList.add('open');
    overlay.setAttribute('aria-hidden','false');
    setTimeout(()=>document.getElementById('staffFullName')?.focus(), 50);
    // capture on window so main.js's document-level Escape handler can't close the modal past the dirty check
    window.addEventListener('keydown', escHandler, true);
  }

  function closeModal(){
    const overlay = document.getElementById('addStaffModal');
    if (!overlay) return;
    overlay.classList.remove('open');
    overlay.style.display = 'none';
    overlay.setAttribute('aria-hidden','true');
    window.removeEventListener('keydown', escHandler, true);
    clearFormErrors();
  }

  function escHandler(e){
    if (e.key !== 'Escape') return;
    e.stopPropagation();
    tryClose();
  }
  function tryClose(){ if (formDirty && !confirm(t('DiscardChanges'))) return; formDirty = false; closeModal(); }

  // forms.css only shows .form-error inside a .form-group.has-error
  function clearFormErrors(){
    document.querySelectorAll('#staffForm .form-group.has-error').forEach(g => g.classList.remove('has-error'));
    document.querySelectorAll('#staffForm .form-error').forEach(el => { el.textContent = ''; });
  }

  function showFieldErrors(errors){
    Object.keys(errors).forEach(k => {
      const el = document.getElementById('err_'+k);
      if (!el) return;
      el.textContent = errors[k];
      el.closest('.form-group')?.classList.add('has-error');
    });
    const first = Object.keys(errors)[0];
    if (first) document.getElementById('staff' + fieldToId(first))?.focus();
  }

  function fieldToId(name){
    const map = { full_name: 'FullName', mobile: 'Mobile', email: 'Email', date_of_birth: 'DOB', department_id: 'Department', designation: 'Designation', join_date: 'JoinDate', gender: 'Gender', shift: 'Shift', status: 'Status' };
    return map[name] || name;
  }

  function prepareForm(){
    clearFormErrors();
    populateDeptAndShifts();
    const today = new Date();
    const iso = `${today.getFullYear()}-${String(today.getMonth()+1).padStart(2,'0')}-${String(today.getDate()).padStart(2,'0')}`;
    document.getElementById('staffDOB').setAttribute('max', iso);
    document.getElementById('staffJoinDate').setAttribute('max', iso);
  }

  function openCreate(){
    formDirty = false;
    const modal = document.querySelector('#addStaffModal .modal');
    document.getElementById('staffModalTitle').textContent = modal.dataset.addTitle;
    document.getElementById('staffSubmitBtnText').textContent = modal.dataset.registerText;
    document.getElementById('staffForm').reset();
    prepareForm();
    document.getElementById('staffEmployeeId').value = '';   // placeholder shows "Auto-generated"
    document.getElementById('staffInternalId').value = '';
    document.getElementById('staffGender').value = 'Female';
    // new staff always start ACTIVE; status is only editable on existing records
    document.getElementById('staffStatusGroup').style.display = 'none';
    if (shifts.length) document.getElementById('staffShift').value = shifts[0].id;
    openModal();
  }

  function openEdit(empId){
    formDirty = false;
    const s = staff.find(x => x.id === empId);
    if (!s){ showToast(t('Error'), t('StaffNotFound'), 'error'); return; }
    const modal = document.querySelector('#addStaffModal .modal');
    document.getElementById('staffModalTitle').textContent = modal.dataset.editTitle;
    document.getElementById('staffSubmitBtnText').textContent = modal.dataset.updateText;
    prepareForm();
    document.getElementById('staffInternalId').value = s.id;
    document.getElementById('staffEmployeeId').value = s.id;
    document.getElementById('staffFullName').value = s.full_name || '';
    document.getElementById('staffMobile').value = s.mobile || '';
    document.getElementById('staffEmail').value = s.email || '';
    document.getElementById('staffDOB').value = s.date_of_birth || '';
    document.getElementById('staffGender').value = s.gender || 'Female';
    document.getElementById('staffDepartment').value = s.department_id || '';
    document.getElementById('staffDesignation').value = s.designation || '';
    document.getElementById('staffJoinDate').value = s.join_date || '';
    // shift codes are matched case-insensitively ("Morning" -> "MORNING"); a value not in the Shift table
    // (older records, e.g. "Night") is kept as an extra option so the record can still be saved unchanged
    const shiftSel = document.getElementById('staffShift');
    const match = shifts.find(x => String(x.id).toLowerCase() === String(s.shift || '').toLowerCase());
    if (match) shiftSel.value = match.id;
    else if (s.shift){ shiftSel.insertAdjacentHTML('beforeend', `<option value="${esc(s.shift)}">${esc(s.shift)}</option>`); shiftSel.value = s.shift; }
    else shiftSel.value = shifts.length ? shifts[0].id : '';
    document.getElementById('staffStatusGroup').style.display = '';
    document.getElementById('staffStatus').value = String(s.status || 'ACTIVE').toUpperCase();
    openModal();
  }

  // ── Validation (mirrors StaffController.ValidateStaffAsync) ──
  function parseIsoDate(v){
    const m = /^(\d{4})-(\d{2})-(\d{2})$/.exec(v || '');
    return m ? new Date(+m[1], +m[2] - 1, +m[3]) : null;
  }

  function validateClient(vals){
    const errors = {};
    const today = new Date(); today.setHours(0,0,0,0);
    if (vals.full_name.length < 2 || vals.full_name.length > 100) errors.full_name = t('ValFullName');
    if (!/^[0-9]{9,12}$/.test(vals.mobile)) errors.mobile = t('ValMobile');
    if (vals.email && !/^[^@\s]+@[^@\s]+\.[^@\s]+$/.test(vals.email)) errors.email = t('ValEmail');
    const dob = parseIsoDate(vals.date_of_birth);
    if (!dob) errors.date_of_birth = t('ValDobRequired');
    else if (dob > today) errors.date_of_birth = t('ValDobFuture');
    else if (new Date(dob.getFullYear() + 18, dob.getMonth(), dob.getDate()) > today) errors.date_of_birth = t('ValMinAge');
    if (!vals.department_id) errors.department_id = t('ValDepartment');
    if (!vals.designation) errors.designation = t('ValDesignation');
    const join = parseIsoDate(vals.join_date);
    if (!join) errors.join_date = t('ValJoinRequired');
    else if (join > today) errors.join_date = t('ValJoinFuture');
    else if (dob && !errors.date_of_birth && join < new Date(dob.getFullYear() + 18, dob.getMonth(), dob.getDate())) errors.join_date = t('ValJoinAfterDob');
    return errors;
  }

  function getFormValues(){
    const v = id => (document.getElementById(id).value || '').trim();
    return {
      id: v('staffInternalId'),
      full_name: v('staffFullName'),
      mobile: v('staffMobile'),
      email: v('staffEmail') || null,
      date_of_birth: v('staffDOB'),
      gender: v('staffGender'),
      department_id: v('staffDepartment'),
      designation: v('staffDesignation'),
      join_date: v('staffJoinDate'),
      shift: v('staffShift'),
      status: v('staffStatus'),
    };
  }

  function antiForgeryToken(){
    const el = document.querySelector('#staffForm input[name="__RequestVerificationToken"]');
    return el ? el.value : '';
  }

  // ── Server calls ──────────────────────────────────────────────
  async function submitCreateOrUpdate(e){
    if (e) e.preventDefault();
    if (submitting) return;
    clearFormErrors();
    const vals = getFormValues();
    const clientErrors = validateClient(vals);
    if (Object.keys(clientErrors).length){ showFieldErrors(clientErrors); return; }
    const isCreate = !vals.id;
    const url = isCreate ? '/Staff/Create' : '/Staff/Update/' + encodeURIComponent(vals.id);
    const body = {
      fullName: vals.full_name, mobile: vals.mobile, email: vals.email,
      dateOfBirth: vals.date_of_birth, gender: vals.gender, departmentId: vals.department_id,
      designation: vals.designation, joinDate: vals.join_date, shift: vals.shift
    };
    if (!isCreate) body.status = vals.status;
    const submitBtn = document.getElementById('staffSubmitBtn');
    submitting = true;
    submitBtn.disabled = true;
    try {
      const res = await fetch(url, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json', 'RequestVerificationToken': antiForgeryToken() },
        body: JSON.stringify(body)
      });
      if (res.status === 400){
        const json = await res.json().catch(() => null);
        if (json && json.errors){ showFieldErrors(json.errors); return; }
        showToast(t('Error'), t('CheckForm'), 'error');
        return;
      }
      if (res.status === 404){ showToast(t('Error'), t('StaffNotFound'), 'error'); return; }
      if (!res.ok){ showToast(t('Error'), `${t('CouldNotSave')} (${res.status})`, 'error'); return; }
      showToast(t('Success'), isCreate ? t('StaffRegistered') : t('StaffUpdated'), 'success');
      formDirty = false;
      closeModal();
      await indexData();
    } catch (err){
      console.error('StaffModule.submit error', err);
      showToast(t('Error'), t('CouldNotReachServer'), 'error');
    } finally {
      submitting = false;
      submitBtn.disabled = false;
    }
  }

  async function deleteStaff(empId){
    const s = staff.find(x => x.id === empId);
    if (!s){ showToast(t('Error'), t('StaffNotFound'), 'error'); return; }
    if (!confirm(t('ConfirmDeleteStaff', s.full_name, s.id))) return;
    try {
      const res = await fetch('/Staff/Delete/' + encodeURIComponent(empId), {
        method: 'POST',
        headers: { 'RequestVerificationToken': antiForgeryToken() }
      });
      if (res.status === 404){ showToast(t('Error'), t('StaffNotFound'), 'error'); await indexData(); return; }
      if (!res.ok){ showToast(t('Error'), `${t('CouldNotDelete')} (${res.status})`, 'error'); return; }
      showToast(t('Success'), t('StaffDeleted'), 'success');
      await indexData();
    } catch (err){
      console.error('StaffModule.delete error', err);
      showToast(t('Error'), t('CouldNotReachServer'), 'error');
    }
  }

  function currentFilters(){
    const val = id => { const el = document.getElementById(id); return el ? (el.value || '').trim() : ''; };
    return { q: val('staffSearch'), departmentId: val('staffFilterDepartment'), status: val('staffFilterStatus') };
  }

  function buildQueryString(params){
    return Object.keys(params).filter(k => params[k]).map(k => encodeURIComponent(k) + '=' + encodeURIComponent(params[k])).join('&');
  }

  async function indexData(){
    const seq = ++loadSeq;
    try{
      const qs = buildQueryString(currentFilters());
      const res = await fetch('/Staff/IndexData' + (qs ? ('?' + qs) : ''));
      if (!res.ok) { console.warn('StaffModule.indexData: server returned', res.status); return false; }
      const json = await res.json();
      if (seq !== loadSeq) return true;   // a newer request is in flight; let it render
      depts = (json.depts || []).map(d => ({ id: d.id, name: d.name, count: d.count || 0, color: d.color || '#2558a8' }));
      shifts = (json.shifts || []).map(s => ({ id: s.id, name: s.name }));
      staff = (json.staff || []).map(s => ({
        id: s.employeeId,
        full_name: s.fullName || '',
        department_id: s.departmentId || '',
        department_name: s.departmentName || '',
        designation: s.designation || '',
        mobile: s.mobile || '',
        email: s.email || null,
        date_of_birth: s.dateOfBirth || '',
        gender: s.gender || 'Female',
        join_date: s.joinDate || '',
        shift: s.shift || '',
        status: s.status || 'ACTIVE'
      }));
      renderStats(json.stats);
      renderTable();
      renderDeptCards();
      populateDeptAndShifts();
      try{
        if (window.IMSBCharts && typeof window.IMSBCharts.StaffDeptChart === 'function'){
          window.IMSBCharts.StaffDeptChart('staffDeptChart', depts.map(d=>d.name), depts.map(d=>d.count || 0));
        }
      } catch(e){ console.warn('Failed to render staff dept chart', e); }
      return true;
    } catch(err){
      console.warn('StaffModule.indexData error', err);
      return false;
    }
  }

  function exportData(){
    const qs = buildQueryString(currentFilters());
    window.location = '/Staff/Export' + (qs ? ('?' + qs) : '');
  }

  function debounce(fn, wait){
    let timer;
    return function(...args){ clearTimeout(timer); timer = setTimeout(() => fn.apply(this, args), wait); };
  }

  window.StaffModule = { openCreate, openEdit, indexData, exportData };

  window.addEventListener('beforeunload', function(e){ if (formDirty){ e.preventDefault(); e.returnValue = ''; } });

  document.addEventListener('DOMContentLoaded', function(){
    const form = document.getElementById('staffForm');
    if (!form) return;
    const overlay = document.getElementById('addStaffModal');
    overlay.style.display = 'none';
    // backdrop click: run the dirty check instead of main.js's unconditional close
    overlay.addEventListener('click', e => {
      if (e.target !== overlay) return;
      e.stopImmediatePropagation();
      tryClose();
    }, true);

    // wired once here (not on every modal open)
    document.querySelectorAll('[data-modal-open="addStaffModal"]').forEach(btn =>
      btn.addEventListener('click', e => { e.preventDefault(); openCreate(); }));
    document.getElementById('staffSubmitBtn').addEventListener('click', submitCreateOrUpdate);
    form.addEventListener('submit', submitCreateOrUpdate);
    form.addEventListener('input', () => { formDirty = true; });
    form.addEventListener('change', () => { formDirty = true; });
    document.getElementById('staffModalClose').addEventListener('click', tryClose);
    document.getElementById('staffCancelBtn').addEventListener('click', e => { e.preventDefault(); tryClose(); });
    document.getElementById('staffExportBtn')?.addEventListener('click', e => { e.preventDefault(); exportData(); });

    const reload = () => indexData();
    document.getElementById('staffSearch')?.addEventListener('input', debounce(reload, 300));
    document.getElementById('staffFilterDepartment')?.addEventListener('change', reload);
    document.getElementById('staffFilterStatus')?.addEventListener('change', reload);

    indexData();
  });
})();
