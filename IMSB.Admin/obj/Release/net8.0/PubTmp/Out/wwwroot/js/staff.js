// Staff / HR page: list, filters, add / edit / delete through the /Staff endpoints.
// Employees are SERP.tbl_Employee rows: EmpTypeID is a SERP.tbl_EMP_Types row, Desig a dbo.departments row (the staff
// category), ShiftID a dbo.Shift row and AgencyID a SERP.tbl_ArtisansTypeAgency row.
// Localized strings come from window.StaffI18n (rendered by Views/Staff/Index.cshtml from SharedResource.*.resx).
(function StaffModule(){
  let employeeTypes = [];
  let depts = [];
  let agencies = [];
  let staff = [];
  let shifts = [];
  let leaveTypes = [];
  let designations = [];   // tbl_Designation rows: { id, name, employee_type_id, salary (admins only) }
  let quotaLoadSeq = 0;   // ignores a quota response for a form that has since been reopened
  // the photo change of the open form: a new JPEG data URL, or a request to remove the one on record
  let photo = { data: null, remove: false };
  let formDirty = false;
  let submitting = false;
  let loadSeq = 0;   // ignores out-of-order responses when filters change quickly

  const GENDERS = ['Female', 'Male', 'Other'];
  const PHOTO_MAX_SIDE = 480;           // px; pictures are shrunk to this before upload
  const PHOTO_MAX_BYTES = 500 * 1024;   // as EmployeePhotos.MaxBytes on the server

  const photoUrl = id => '/Staff/Photo/' + encodeURIComponent(id) + '?v=' + Date.now();   // never a stale cached photo

  function isoToday(){
    const d = new Date();
    return `${d.getFullYear()}-${String(d.getMonth()+1).padStart(2,'0')}-${String(d.getDate()).padStart(2,'0')}`;
  }

  // "2020-01-15" -> "15 Jan 2020" in the page language; parsed as a local date so it never shifts a day
  function formatDateDisplay(iso){
    const m = /^(\d{4})-(\d{2})-(\d{2})$/.exec(iso || '');
    if (!m) return iso || '';
    const d = new Date(+m[1], +m[2] - 1, +m[3]);
    return `${m[3]} ${d.toLocaleString(document.documentElement.lang || undefined, { month: 'short' })} ${m[1]}`;
  }

  function formatSalary(value){
    if (value == null || value === '' || !Number.isFinite(Number(value))) return '';
    return new Intl.NumberFormat(document.documentElement.lang || 'en-IN', { style: 'currency', currency: 'INR', maximumFractionDigits: 2 }).format(Number(value));
  }

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
    if (s === 'INACTIVE') return t('Inactive');
    return status || '';
  }

  function statusBadge(status){
    return String(status || '').toUpperCase() === 'ACTIVE' ? 'badge-success' : 'badge-danger';
  }

  function genderLabel(g){
    return GENDERS.includes(g) ? t(g) : (g || '');
  }

  const findStaff = id => staff.find(x => String(x.id) === String(id));

  function viewStaff(empId){
    const s = findStaff(empId);
    if (!s){ showToast(t('Error'), t('StaffNotFound'), 'error'); return; }
    const fields = [
      ['EmployeeID', s.id],
      ['FullName', s.full_name],
      ['Department', s.department_name],
      ['Designation', s.designation_name],
      ['EmployeeType', s.employee_type_name],
      ['Mobile', s.mobile],
      ['AadhaarNumber', s.aadhaar_masked],
      ['Gender', genderLabel(s.gender)],
      ['Shift', s.shift_name],
      ['JoinDate', formatDateDisplay(s.join_date)],
      ['Status', statusLabel(s.status)],
      ['Remarks', s.remarks]
    ];
    if (window.StaffCanViewSalary) fields.splice(fields.length - 2, 0, ['MonthlySalary', formatSalary(s.monthly_salary)]);
    const picture = s.has_photo
      ? `<img class="staff-profile-photo" src="${esc(photoUrl(s.id))}" alt="${esc(t('Photo'))}" />`
      : '';
    document.getElementById('staffProfileBody').innerHTML = `${picture}
      <div class="form-grid col-2">
        ${fields.map(([label, value]) => `<div class="form-group"><div class="form-label">${esc(t(label))}</div><div style="font-weight:600;overflow-wrap:anywhere">${esc(value == null || value === '' ? '—' : value)}</div></div>`).join('')}
      </div>`;
    const overlay = document.getElementById('staffProfileModal');
    overlay.classList.add('open');
    overlay.setAttribute('aria-hidden', 'false');
  }

  // ── Rendering ─────────────────────────────────────────────────
  function renderTable(){
    const staffBody = document.getElementById('staffBody');
    if (!staffBody) return;
    const rows = staff.map(s => `
      <tr>
        <td><code style="font-size:.7rem">${esc(s.id)}</code></td>
        <td><div style="display:flex;align-items:center;gap:8px"><div style="width:30px;height:30px;border-radius:50%;background:var(--primary-xlight);display:flex;align-items:center;justify-content:center;font-size:.65rem;font-weight:700;color:var(--primary)">${esc((s.full_name||'').split(' ').map(x=>x[0]||'').join('').slice(0,2))}</div><strong>${esc(s.full_name)}</strong></div></td>
        <td>${esc(s.employee_type_name || '—')}</td>
        <td>${esc(s.department_name || '—')}</td>
        <td>${esc(s.designation_name || '—')}</td>
        <td>${esc(s.shift_name || '—')}</td>
        <td><span class="badge ${statusBadge(s.status)}">${esc(statusLabel(s.status))}</span></td>
        <td style="white-space:nowrap">
          <button type="button" class="btn btn-ghost btn-sm btn-icon" data-view-staff="${esc(s.id)}" title="${esc(t('StaffProfile'))}" aria-label="${esc(t('StaffProfile'))}" style="color:var(--primary-light)"><i class="fa-solid fa-circle-info"></i></button>
          <button type="button" class="btn btn-ghost btn-sm btn-icon" data-edit-staff="${esc(s.id)}" title="${esc(t('Edit'))}"><i class="fa-solid fa-pen"></i></button>
          <button type="button" class="btn btn-ghost btn-sm btn-icon" data-delete-staff="${esc(s.id)}" title="${esc(t('Delete'))}" style="color:var(--danger)"><i class="fa-solid fa-trash"></i></button>
        </td>
      </tr>
    `).join('');
    const columnCount = 8;
    staffBody.innerHTML = rows || `<tr><td colspan="${columnCount}" style="text-align:center;color:var(--text-muted);padding:18px">${esc(t('NoStaffFound'))}</td></tr>`;
    staffBody.querySelectorAll('[data-edit-staff]').forEach(b => b.addEventListener('click', () => openEdit(b.dataset.editStaff)));
    staffBody.querySelectorAll('[data-delete-staff]').forEach(b => b.addEventListener('click', () => deleteStaff(b.dataset.deleteStaff)));
    staffBody.querySelectorAll('[data-view-staff]').forEach(b => b.addEventListener('click', () => viewStaff(b.dataset.viewStaff)));
  }

  function renderDeptCards(){
    const deptCards = document.getElementById('deptCards');
    if (!deptCards) return;
    // only departments that have staff; tbl_Designation can hold many unused rows
    const used = depts.filter(d => d.count > 0);
    if (!used.length){ deptCards.innerHTML = `<div style="text-align:center;color:var(--text-muted);font-size:.78rem;padding:12px">${esc(t(depts.length ? 'NoStaffFound' : 'NoDepartmentsConfigured'))}</div>`; return; }
    // one bar row per department: bar length relative to the largest department, % of all staff alongside
    const total = used.reduce((sum, d) => sum + d.count, 0);
    const max = Math.max(...used.map(d => d.count), 1);
    deptCards.innerHTML = used.sort((a, b) => b.count - a.count).map(d => {
      const pct = total ? Math.round(d.count / total * 100) : 0;
      return `
      <div class="dept-bar-row" title="${esc(t('PercentOfTotal', pct))}">
        <div class="dept-bar-name">${esc(d.name)}</div>
        <div class="dept-bar-track"><div class="dept-bar-fill" style="width:${Math.round(d.count / max * 100)}%;background:${esc(d.color)}"></div></div>
        <div class="dept-bar-count">${d.count}</div>
        <div class="dept-bar-pct">${pct}%</div>
      </div>`;
    }).join('');
  }

  function renderStats(stats){
    if (!stats) return;
    const set = (id, v) => { const el = document.getElementById(id); if (el) el.textContent = v; };
    set('statTotalStaff', stats.total);
    set('statActiveStaff', stats.active);
    set('statInactiveStaff', stats.inactive);
    set('statDepartments', stats.departments);
    set('statAgencyStaff', stats.agencyStaff);
  }

  const optionsHtml = list => list.map(x => `<option value="${esc(x.id)}">${esc(x.name)}</option>`).join('');

  // a select rebuilt from a list keeps its current value when that value is still offered
  function fillSelect(id, firstText, html){
    const sel = document.getElementById(id);
    if (!sel) return;
    const current = sel.value;
    sel.innerHTML = `<option value="">${esc(firstText)}</option>` + html;
    sel.value = current;
  }

  // employee types: SERP.tbl_EMP_Types; staff categories: dbo.departments; shifts: dbo.Shift (the offered ones)
  function populateSelects(){
    fillSelect('staffEmployeeType', t('SelectPlaceholder'), optionsHtml(employeeTypes));
    fillSelect('staffDepartment', t(depts.length ? 'SelectPlaceholder' : 'NoDepartmentsConfigured'), optionsHtml(depts));
    fillSelect('staffShift', t(shifts.length ? 'SelectPlaceholder' : 'NoShift'), optionsHtml(shifts));
    fillSelect('staffAgency', t('SelectPlaceholder'), optionsHtml(agencies));
    fillSelect('staffFilterEmployeeType', t('AllEmployeeTypes'), optionsHtml(employeeTypes));
    fillSelect('staffFilterDepartment', t('AllDepartments'), optionsHtml(depts));
    // the Designation box suggests the names already in tbl_Designation; whatever is typed is saved in the employee's own row
    const names = [...new Set(designations.map(d => d.name))].sort((a, b) => a.localeCompare(b));
    document.getElementById('staffDesignationList').innerHTML = names.map(n => `<option value="${esc(n)}"></option>`).join('');
  }

  // keeps a value on record that is no longer offered (e.g. saved by the SERP app) so the record can be saved unchanged
  function keepValue(sel, value, all){
    if (!value) return;
    if (![...sel.options].some(o => o.value === String(value))){
      const known = all.find(x => String(x.id) === String(value));
      sel.insertAdjacentHTML('beforeend', `<option value="${esc(value)}">${esc(known ? known.name : value)}</option>`);
    }
    sel.value = String(value);
  }

  // puts a record's staff category and shift into the form; a value not offered (a shift no longer in use, or an id
  // saved by the SERP app) is kept as an extra option so the record can be saved unchanged
  function setDepartmentAndShift(s){
    const dsel = document.getElementById('staffDepartment');
    const ssel = document.getElementById('staffShift');
    dsel.value = '';
    ssel.value = '';
    keepValue(dsel, s.department_id, depts);
    if (s.department_id && !depts.some(d => String(d.id) === String(s.department_id)) && s.department_name)
      dsel.selectedOptions[0].textContent = s.department_name;
    keepValue(ssel, s.shift_id, shifts);
    if (s.shift_id && !shifts.some(x => String(x.id) === String(s.shift_id)) && s.shift_name)
      ssel.selectedOptions[0].textContent = s.shift_name;
  }

  // ── Photo (tbl_Employee.ImageBase64) ──────────────────────────
  function showPhoto(src){
    const img = document.getElementById('staffPhotoPreview');
    img.style.display = src ? '' : 'none';
    if (src) img.src = src; else img.removeAttribute('src');
    document.getElementById('staffPhotoPlaceholder').style.display = src ? 'none' : '';
    document.getElementById('staffPhotoRemove').style.display = src ? '' : 'none';
  }

  function resetPhoto(s){
    photo = { data: null, remove: false };
    document.getElementById('staffPhoto').value = '';
    showPhoto(s && s.has_photo ? photoUrl(s.id) : null);
  }

  // shrinks the picture to at most PHOTO_MAX_SIDE px and re-encodes it as JPEG, so uploads stay small
  function shrinkPhoto(file){
    return new Promise((resolve, reject) => {
      const reader = new FileReader();
      reader.onerror = () => reject(new Error('read failed'));
      reader.onload = () => {
        const img = new Image();
        img.onerror = () => reject(new Error('not an image'));
        img.onload = () => {
          const scale = Math.min(1, PHOTO_MAX_SIDE / Math.max(img.width, img.height));
          const canvas = document.createElement('canvas');
          canvas.width = Math.max(1, Math.round(img.width * scale));
          canvas.height = Math.max(1, Math.round(img.height * scale));
          const ctx = canvas.getContext('2d');
          ctx.fillStyle = '#fff';   // PNG transparency becomes white, not black
          ctx.fillRect(0, 0, canvas.width, canvas.height);
          ctx.drawImage(img, 0, 0, canvas.width, canvas.height);
          resolve(canvas.toDataURL('image/jpeg', 0.85));
        };
        img.src = reader.result;
      };
      reader.readAsDataURL(file);
    });
  }

  // bytes of a base64 data URL
  const dataUrlBytes = url => Math.floor((url.length - url.indexOf(',') - 1) * 3 / 4);

  async function onPhotoChosen(e){
    const file = e.target.files && e.target.files[0];
    const err = document.getElementById('err_photo');
    err.textContent = '';
    err.closest('.form-group').classList.remove('has-error');
    if (!file) return;
    const fail = () => { err.textContent = t('ValPhoto'); err.closest('.form-group').classList.add('has-error'); e.target.value = ''; };
    if (!/^image\/(jpeg|png)$/.test(file.type)) { fail(); return; }
    try {
      const data = await shrinkPhoto(file);
      if (dataUrlBytes(data) > PHOTO_MAX_BYTES) { fail(); return; }
      photo = { data, remove: false };
      showPhoto(data);
      formDirty = true;
    } catch (ex){
      console.warn('StaffModule.photo', ex);
      fail();
    }
  }

  function onPhotoRemove(){
    photo = { data: null, remove: true };
    document.getElementById('staffPhoto').value = '';
    showPhoto(null);
    formDirty = true;
  }

  // ── Annual leave quotas (employee_leave_quotas) ───────────────
  // one days field per leave type, filled from the quotas on record when editing
  function renderLeaveQuotas(types, rows){
    const onRecord = new Map((rows || []).map(r => [String(r.leaveType).toLowerCase(), r]));
    document.getElementById('staffLeaveQuotaSection').style.display = types.length ? '' : 'none';
    document.getElementById('staffLeaveQuotaFields').innerHTML = types.map((type, i) => {
      const row = onRecord.get(String(type).toLowerCase());
      const days = row ? String(Number(row.annualQuota)) : '';
      return `
      <div class="form-group">
        <label class="form-label" for="staffLeaveQuota${i}">${esc(type)}</label>
        <input id="staffLeaveQuota${i}" class="form-control" type="number" min="0" max="366" step="0.5" data-leave-type="${esc(type)}" data-original="${esc(days)}" value="${esc(days)}" placeholder="${esc(t('Days'))}" />
        ${row ? `<div class="form-hint" style="font-size:.7rem;color:var(--text-muted)">${esc(t('Taken'))}: ${esc(Number(row.taken))} · ${esc(t('Balance'))}: ${esc(Number(row.balance))}</div>` : ''}
      </div>`;
    }).join('');
  }

  async function loadEditQuotas(empId, seq){
    try {
      const res = await fetch('/Staff/LeaveQuotas/' + encodeURIComponent(empId), { cache: 'no-store' });
      if (!res.ok) throw new Error(`Quota load failed (${res.status})`);
      const json = await res.json();
      if (seq === quotaLoadSeq) renderLeaveQuotas(leaveTypes, json.rows || []);
    } catch (err){
      console.error('StaffModule.loadEditQuotas error', err);
      if (seq !== quotaLoadSeq) return;
      document.getElementById('staffLeaveQuotaSection').style.display = '';
      showFieldErrors({ leave_quotas: t('CouldNotReachServer') });
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
    const map = { full_name: 'FullName', mobile: 'Mobile', aadhaar: 'Aadhaar', employee_type_id: 'EmployeeType', department_id: 'Department', agency_id: 'Agency', gender: 'Gender', shift: 'Shift', stall_number: 'StallNumber', status: 'Status', remarks: 'Remarks',
      join_date: 'JoinDate', monthly_salary: 'MonthlySalary', leave_quotas: 'LeaveQuota0', photo: 'PhotoPick' };
    return map[name] || name;
  }

  // a value on record outside the list (e.g. "M" saved by the SERP app) is kept as an extra option
  function setGender(value){
    const sel = document.getElementById('staffGender');
    sel.querySelectorAll('option[data-legacy]').forEach(o => o.remove());
    if (value && !GENDERS.includes(value)) sel.insertAdjacentHTML('beforeend', `<option value="${esc(value)}" data-legacy>${esc(value)}</option>`);
    sel.value = value || 'Female';
  }

  // the number is never sent back: registering needs one, editing keeps the one on record unless a new one is typed
  function prepareAadhaar(s){
    document.getElementById('staffAadhaar').value = '';
    document.getElementById('staffAadhaarReq').style.display = s ? 'none' : '';
    document.getElementById('staffAadhaarHint').textContent = s && s.aadhaar_masked ? t('AadhaarKeepHint', s.aadhaar_masked) : t('AadhaarMaskedHint');
  }

  function openCreate(){
    formDirty = false;
    const modal = document.querySelector('#addStaffModal .modal');
    document.getElementById('staffModalTitle').textContent = modal.dataset.addTitle;
    document.getElementById('staffSubmitBtnText').textContent = modal.dataset.registerText;
    document.getElementById('staffForm').reset();
    clearFormErrors();
    populateSelects();
    prepareAadhaar(null);
    document.getElementById('staffEmployeeId').value = '';   // placeholder shows "Auto-generated"
    document.getElementById('staffInternalId').value = '';
    setGender('Female');
    // a single employee type needs no choosing
    if (employeeTypes.length === 1) document.getElementById('staffEmployeeType').value = employeeTypes[0].id;
    // new staff always start ACTIVE; status and remarks are only editable on existing records
    document.getElementById('staffStatusGroup').style.display = 'none';
    document.getElementById('staffRemarksGroup').style.display = 'none';
    prepareJoinDate(null);
    resetPhoto(null);
    quotaLoadSeq++;
    renderLeaveQuotas(leaveTypes, []);
    openModal();
  }

  // required when registering; a SERP record without one may stay without one
  function prepareJoinDate(s){
    const input = document.getElementById('staffJoinDate');
    input.setAttribute('max', isoToday());
    input.value = s ? (s.join_date || '') : '';
    document.getElementById('staffJoinDateReq').style.display = !s || s.join_date ? '' : 'none';
    // designation (tbl_Designation) and its salary; a record without a designation or staff category may stay without
    document.getElementById('staffDesignation').value = s ? (s.designation_name || '') : '';
    document.getElementById('staffDesignationReq').style.display = !s || s.designation_id ? '' : 'none';
    document.getElementById('staffDepartmentReq').style.display = !s || s.department_id ? '' : 'none';
    const salary = document.getElementById('staffMonthlySalary');
    if (salary) salary.value = s && s.monthly_salary != null ? String(s.monthly_salary) : '';
  }

  function openEdit(empId){
    formDirty = false;
    const s = findStaff(empId);
    if (!s){ showToast(t('Error'), t('StaffNotFound'), 'error'); return; }
    const modal = document.querySelector('#addStaffModal .modal');
    document.getElementById('staffModalTitle').textContent = modal.dataset.editTitle;
    document.getElementById('staffSubmitBtnText').textContent = modal.dataset.updateText;
    clearFormErrors();
    populateSelects();
    prepareAadhaar(s);
    document.getElementById('staffInternalId').value = s.id;
    document.getElementById('staffEmployeeId').value = s.id;
    document.getElementById('staffFullName').value = s.full_name || '';
    document.getElementById('staffMobile').value = s.mobile || '';
    setGender(s.gender);
    const typeSel = document.getElementById('staffEmployeeType');
    typeSel.value = '';
    keepValue(typeSel, s.employee_type_id, employeeTypes);
    setDepartmentAndShift(s);
    document.getElementById('staffAgency').value = s.agency_id || '';
    document.getElementById('staffStallNumber').value = s.stall_number == null ? '' : String(s.stall_number);
    document.getElementById('staffStatusGroup').style.display = '';
    document.getElementById('staffRemarksGroup').style.display = '';
    document.getElementById('staffStatus').value = String(s.status || 'ACTIVE').toUpperCase();
    document.getElementById('staffRemarks').value = s.remarks || '';
    prepareJoinDate(s);
    resetPhoto(s);
    renderLeaveQuotas([], []);
    loadEditQuotas(s.id, ++quotaLoadSeq);
    openModal();
  }

  // ── Validation (mirrors IMSB.Backend.StaffValidation) ──
  function validateClient(vals){
    const errors = {};
    if (vals.full_name.length < 2 || vals.full_name.length > 100) errors.full_name = t('ValFullName');
    // an unchanged number is accepted so records saved under older rules can still be edited
    const saved = vals.id ? findStaff(vals.id) : null;
    if (!(saved && vals.mobile && vals.mobile === saved.mobile) && !IMSBValidate.mobile(vals.mobile)) errors.mobile = t('ValMobile');
    // the server has the final say (duplicates)
    if ((!vals.id || vals.aadhaar) && !IMSBValidate.aadhaar(vals.aadhaar)) errors.aadhaar = t('ValAadhaar');
    if (!vals.employee_type_id) errors.employee_type_id = t('ValEmployeeType');
    if (!vals.department_id && (!saved || saved.department_id)) errors.department_id = t('ValDepartment');
    if (!vals.designation ? (!saved || saved.designation_id) : vals.designation.length > 50) errors.designation = t('ValDesignation');
    if (!(saved && vals.gender === saved.gender) && !GENDERS.includes(vals.gender)) errors.gender = t('ValGender');
    if (vals.stall_number !== ''){
      const n = Number(vals.stall_number);
      if (!Number.isInteger(n) || n < 1 || n > 99999) errors.stall_number = t('ValStallNumber');
    }
    if (vals.remarks.length > 200) errors.remarks = t('ValEmployeeRemarks');
    if (!vals.join_date){
      if (!saved || saved.join_date) errors.join_date = t('ValJoinRequired');
    }
    else if (!/^\d{4}-\d{2}-\d{2}$/.test(vals.join_date)) errors.join_date = t('ValJoinRequired');
    else if (vals.join_date > isoToday()) errors.join_date = t('ValJoinFuture');
    if (vals.monthly_salary !== null && vals.monthly_salary !== ''){
      const salary = Number(vals.monthly_salary);
      if (!Number.isFinite(salary) || salary < 0 || salary > 99999999.99) errors.monthly_salary = t('ValDesignationSalary');
    }
    if (vals.leave_quotas.some(q => q.annualQuota !== null && (!Number.isInteger(q.annualQuota * 2) || q.annualQuota < 0 || q.annualQuota > 366)))
      errors.leave_quotas = t('ValLeaveQuotas');
    return errors;
  }

  function getFormValues(){
    const v = id => (document.getElementById(id).value || '').trim();
    const stall = document.getElementById('staffStallNumber');
    return {
      id: v('staffInternalId'),
      full_name: v('staffFullName'),
      mobile: v('staffMobile'),
      aadhaar: v('staffAadhaar') || null,
      gender: v('staffGender'),
      employee_type_id: v('staffEmployeeType'),
      department_id: v('staffDepartment'),
      designation: v('staffDesignation'),
      agency_id: v('staffAgency') || null,
      shift_id: v('staffShift') || null,
      // text the browser can't read as a number must fail validation, not vanish
      stall_number: stall.validity.badInput ? 'x' : v('staffStallNumber'),
      status: v('staffStatus'),
      remarks: v('staffRemarks'),
      join_date: v('staffJoinDate'),
      // null when the field is not on the page (salaries are admin-only)
      monthly_salary: document.getElementById('staffMonthlySalary') ? v('staffMonthlySalary') : null,
      // only changed fields: blank clears a quota on record; text the browser can't read as a number must fail validation, not vanish
      leave_quotas: [...document.querySelectorAll('#staffLeaveQuotaFields input')]
        .filter(i => i.validity.badInput || i.value.trim() !== i.dataset.original)
        .map(i => ({ leaveType: i.dataset.leaveType, annualQuota: i.validity.badInput ? NaN : i.value.trim() === '' ? null : Number(i.value) })),
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
      fullName: vals.full_name, mobile: vals.mobile, aadhaar: vals.aadhaar, gender: vals.gender,
      employeeTypeId: vals.employee_type_id, departmentId: vals.department_id || null, designation: vals.designation || null,
      agencyId: vals.agency_id, shiftId: vals.shift_id,
      stallNumber: vals.stall_number === '' ? null : Number(vals.stall_number),
      joinDate: vals.join_date || null,
      monthlySalary: vals.monthly_salary === null || vals.monthly_salary === '' ? null : Number(vals.monthly_salary),
      // registering sends only the types given days; blank ones simply do not apply
      leaveQuotas: isCreate ? vals.leave_quotas.filter(q => q.annualQuota !== null) : vals.leave_quotas,
      photo: photo.data,
      removePhoto: photo.remove
    };
    if (!isCreate){ body.status = vals.status; body.remarks = vals.remarks; }
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
    const s = findStaff(empId);
    if (!s){ showToast(t('Error'), t('StaffNotFound'), 'error'); return; }
    if (!confirm(t('ConfirmDeleteStaff', s.full_name, s.id))) return;
    try {
      const res = await fetch('/Staff/Delete/' + encodeURIComponent(empId), {
        method: 'POST',
        headers: { 'RequestVerificationToken': antiForgeryToken() }
      });
      if (res.status === 404){ showToast(t('Error'), t('StaffNotFound'), 'error'); await indexData(); return; }
      if (res.status === 409){
        // e.g. attendance is on record: the server says to set the employee Inactive instead
        const json = await res.json().catch(() => null);
        showToast(t('Error'), (json && json.error) || t('CouldNotDelete'), 'error');
        return;
      }
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
    return { q: val('staffSearch'), employeeTypeId: val('staffFilterEmployeeType'), departmentId: val('staffFilterDepartment'), status: val('staffFilterStatus') };
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
      employeeTypes = (json.employeeTypes || []).map(x => ({ id: x.id, name: x.name, count: x.count || 0 }));
      leaveTypes = json.leaveTypes || [];
      depts = (json.depts || []).map(d => ({ id: d.id, name: d.name, count: d.count || 0, color: d.color || '#2558a8' }));
      agencies = (json.agencies || []).map(a => ({ id: a.id, name: a.name }));
      shifts = (json.shifts || []).map(s => ({ id: s.id, name: s.name }));
      designations = (json.designations || []).map(d => ({ id: d.id, name: d.name || '', employee_type_id: d.employeeTypeId || '', salary: d.salary }));
      staff = (json.staff || []).map(s => ({
        id: s.employeeId,
        full_name: s.fullName || '',
        employee_type_id: s.employeeTypeId || '',
        employee_type_name: s.employeeTypeName || '',
        department_id: s.departmentId || '',
        department_name: s.departmentName || '',
        designation_id: s.designationId || '',
        designation_name: s.designationName || '',
        agency_id: s.agencyId || '',
        agency_name: s.agencyName || '',
        mobile: s.mobile || '',
        aadhaar_masked: s.aadhaarMasked || null,
        gender: s.gender || '',
        shift_id: s.shiftId || '',
        shift_name: s.shiftName || '',
        stall_number: s.stallNumber,
        status: s.status || 'ACTIVE',
        remarks: s.remarks || '',
        join_date: s.joinDate || '',
        monthly_salary: s.monthlySalary,
        has_photo: !!s.hasPhoto
      }));
      renderStats(json.stats);
      renderTable();
      renderDeptCards();
      populateSelects();
      try{
        if (window.IMSBCharts && typeof window.IMSBCharts.StaffDeptChart === 'function'){
          const used = depts.filter(d => d.count > 0);
          window.IMSBCharts.StaffDeptChart('staffDeptChart', used.map(d=>d.name), used.map(d=>d.count));
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
    document.getElementById('staffPhotoPick').addEventListener('click', () => document.getElementById('staffPhoto').click());
    document.getElementById('staffPhoto').addEventListener('change', onPhotoChosen);
    document.getElementById('staffPhotoRemove').addEventListener('click', onPhotoRemove);
    document.getElementById('staffModalClose').addEventListener('click', tryClose);
    document.getElementById('staffCancelBtn').addEventListener('click', e => { e.preventDefault(); tryClose(); });
    document.getElementById('staffExportBtn')?.addEventListener('click', e => { e.preventDefault(); exportData(); });

    const reload = () => indexData();
    document.getElementById('staffSearch')?.addEventListener('input', debounce(reload, 300));
    document.getElementById('staffFilterEmployeeType')?.addEventListener('change', reload);
    document.getElementById('staffFilterDepartment')?.addEventListener('change', reload);
    document.getElementById('staffFilterStatus')?.addEventListener('change', reload);

    if (window.StaffOpenCreate) {
      indexData().then(loaded => { if (loaded) openCreate(); });
    } else {
      indexData();
    }
  });
})();
