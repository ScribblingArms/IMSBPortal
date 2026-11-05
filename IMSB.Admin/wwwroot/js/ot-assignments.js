// OT Assignment page: lists each staff member's OT From / To timings, with add, view, edit and delete, through the
// /OtAssignments endpoints. Localized strings come from window.OtAssignmentI18n (rendered by Views/OtAssignments/Index.cshtml).
(function OtAssignmentsModule(){
  let rows = [];         // /OtAssignments/Data rows
  let staff = [];        // staff who can still be given an assignment
  let editingId = null;  // null while adding
  let saving = false;

  const I18N = window.OtAssignmentI18n || {};
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
  const token = () => document.querySelector('#otForm input[name="__RequestVerificationToken"]')?.value || '';

  // ── List ──────────────────────────────────────────────────────
  async function load(){
    try {
      const res = await fetch('/OtAssignments/Data');
      if (!res.ok){ toast(t('Error'), t('CouldNotReachServer'), 'error'); return; }
      const json = await res.json();
      rows = json.rows || [];
      staff = json.staff || [];
      render();
    } catch (e) {
      console.error(e);
      toast(t('Error'), t('CouldNotReachServer'), 'error');
    }
  }

  // yyyy-MM-ddTHH:mm as shown to the user
  const stamp = v => String(v || '').replace('T', ' ');

  function actionButtons(id){
    return [['view', 'fa-eye', t('View'), ''], ['edit', 'fa-pen', t('Edit'), ''], ['delete', 'fa-trash', t('Delete'), 'color:var(--danger)']]
      .map(([act, icon, label, style]) => `<button type="button" class="btn btn-ghost btn-sm btn-icon" data-act="${act}" data-id="${esc(id)}"
          title="${esc(label)}" aria-label="${esc(label)}" style="${style}"><i class="fa-solid ${icon}"></i></button>`)
      .join('');
  }

  function render(){
    const q = $('otSearch').value.trim().toLowerCase();
    const shown = rows.filter(r => !q || `${r.staffName} ${r.employeeId} ${r.staffCategory} ${r.designation}`.toLowerCase().includes(q));
    $('otCount').textContent = shown.length;
    $('otBody').innerHTML = shown.length ? shown.map(r => `<tr>
      <td><code>${esc(r.employeeId)}</code></td>
      <td><strong>${esc(r.staffName)}</strong></td>
      <td>${esc(r.staffCategory || '—')}</td>
      <td>${esc(r.designation || '—')}</td>
      <td style="white-space:nowrap">${esc(stamp(r.fromDateTime))}</td>
      <td style="white-space:nowrap">${esc(stamp(r.toDateTime))}</td>
      <td style="white-space:nowrap">${actionButtons(r.id)}</td>
    </tr>`).join('') : `<tr><td colspan="7" class="empty-state" style="text-align:center">${esc(t('NoOTAssignments'))}</td></tr>`;
  }

  // ── Add / edit form ───────────────────────────────────────────
  function clearErrors(){
    ['request', 'staff_id', 'from_datetime', 'to_datetime'].forEach(k => {
      const el = $('err_' + k);
      el.textContent = '';
      el.style.display = '';
      el.closest('.form-group')?.classList.remove('has-error');
    });
  }
  function showErrors(errors){
    Object.entries(errors).forEach(([k, msg]) => {
      const el = $('err_' + k) || $('err_request');
      el.textContent = msg;
      const group = el.closest('.form-group');
      if (group) group.classList.add('has-error'); else el.style.display = 'block';
    });
  }

  function openForm(row){
    editingId = row ? row.id : null;
    clearErrors();
    $('otFormTitle').textContent = t(row ? 'EditOTAssignment' : 'AddOTAssignment');
    const select = $('otStaff');
    // the staff member of a saved assignment cannot be changed; delete it and add another instead
    select.innerHTML = row
      ? `<option value="${esc(row.staffId)}">${esc(row.employeeId)} – ${esc(row.staffName)}</option>`
      : `<option value="">${esc(t('SelectPlaceholder'))}</option>` +
        staff.map(s => `<option value="${esc(s.id)}">${esc(s.employeeId)} – ${esc(s.fullName)}</option>`).join('');
    select.disabled = !!row;
    $('otFrom').value = row ? row.fromDateTime : '';
    $('otTo').value = row ? row.toDateTime : '';
    openModal('otFormModal');
  }

  // same rules as OtAssignmentMapper.ValidateTimes on the server (yyyy-MM-ddTHH:mm compares as text)
  function validate(body){
    const errors = {};
    if (!editingId && !body.staffId) errors.staff_id = t('ValOtSelectStaff');
    if (!body.fromDateTime) errors.from_datetime = t('ValOtTimes');
    else if (!body.toDateTime) errors.to_datetime = t('ValOtTimes');
    else if (body.toDateTime <= body.fromDateTime) errors.to_datetime = t('ValOtToAfterFrom');
    return errors;
  }

  async function save(){
    if (saving) return;
    clearErrors();
    const body = { staffId: $('otStaff').value || null, fromDateTime: $('otFrom').value, toDateTime: $('otTo').value };
    const errors = validate(body);
    if (Object.keys(errors).length){ showErrors(errors); return; }

    const url = editingId ? '/OtAssignments/Update/' + encodeURIComponent(editingId) : '/OtAssignments/Create';
    saving = true;
    $('otSaveBtn').disabled = true;
    try {
      const res = await fetch(url, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json', 'RequestVerificationToken': token() },
        body: JSON.stringify(body)
      });
      if (res.status === 400){
        const json = await res.json().catch(() => ({}));
        showErrors(json.errors || { request: t('CheckForm') });
        return;
      }
      if (res.status === 404){ toast(t('Error'), t('CheckForm'), 'error'); closeModal('otFormModal'); await load(); return; }
      if (!res.ok){ toast(t('Error'), `${t('CheckForm')} (${res.status})`, 'error'); return; }
      toast(t('Success'), t('OTAssignmentSaved'), 'success');
      closeModal('otFormModal');
      await load();
    } catch (e) {
      console.error(e);
      toast(t('Error'), t('CouldNotReachServer'), 'error');
    } finally {
      saving = false;
      $('otSaveBtn').disabled = false;
    }
  }

  // ── View / delete ─────────────────────────────────────────────
  function view(row){
    const fields = [
      ['EmployeeID', row.employeeId], ['StaffName', row.staffName], ['Department', row.staffCategory], ['Designation', row.designation],
      ['OTFromTime', stamp(row.fromDateTime)], ['OTToTime', stamp(row.toDateTime)], ['UpdatedBy', row.by]
    ];
    $('otViewBody').innerHTML = `<div class="form-grid col-2">
      ${fields.map(([label, value]) => `<div class="form-group"><div class="form-label">${esc(t(label))}</div><div style="font-weight:600;overflow-wrap:anywhere">${esc(value || '—')}</div></div>`).join('')}
    </div>`;
    openModal('otViewModal');
  }

  async function remove(row){
    if (!confirm(t('ConfirmDeleteOTAssignment', row.staffName))) return;
    try {
      const res = await fetch('/OtAssignments/Delete/' + encodeURIComponent(row.id), { method: 'POST', headers: { 'RequestVerificationToken': token() } });
      if (!res.ok && res.status !== 404){ toast(t('Error'), `${t('CheckForm')} (${res.status})`, 'error'); return; }
      if (res.ok) toast(t('Success'), t('OTAssignmentDeleted'), 'success');
      await load();
    } catch (e) {
      console.error(e);
      toast(t('Error'), t('CouldNotReachServer'), 'error');
    }
  }

  document.addEventListener('DOMContentLoaded', () => {
    if (!$('otBody')) return;
    $('otForm').addEventListener('submit', e => { e.preventDefault(); save(); });
    $('otAddBtn').addEventListener('click', () => openForm(null));
    $('otSaveBtn').addEventListener('click', save);
    $('otSearch').addEventListener('input', render);
    $('otBody').addEventListener('click', e => {
      const btn = e.target.closest('[data-act]');
      const row = btn && rows.find(r => r.id === btn.dataset.id);
      if (!row) return;
      if (btn.dataset.act === 'view') view(row);
      else if (btn.dataset.act === 'edit') openForm(row);
      else if (btn.dataset.act === 'delete') remove(row);
    });
    load();
  });
})();
