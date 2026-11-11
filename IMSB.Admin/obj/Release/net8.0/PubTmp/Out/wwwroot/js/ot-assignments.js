// OT Assignment page: lists each staff member's OT date and From / To timings, with add, view, edit and delete, through the
// /OtAssignments endpoints. Localized strings come from window.OtAssignmentI18n (rendered by Views/OtAssignments/Index.cshtml).
// The date is picked from the current month only (today onwards); the OT hours follow from the two times, and a To time
// earlier than the From time ends on the next day. Assignments dated before today are read-only.
(function OtAssignmentsModule(){
  let rows = [];         // /OtAssignments/Data rows
  let staff = [];        // staff who can still be given an assignment
  let editingId = null;  // null while adding
  let saving = false;
  let today = localToday();  // replaced by the server's date on load
  let selectedDate = '';     // yyyy-MM-dd picked in the calendar

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
      if (/^\d{4}-\d{2}-\d{2}$/.test(json.today || '')) today = json.today;
      render();
    } catch (e) {
      console.error(e);
      toast(t('Error'), t('CouldNotReachServer'), 'error');
    }
  }

  // ── Dates and times (yyyy-MM-dd / HH:mm strings, which compare correctly as text) ──
  function localToday(){
    const d = new Date();
    return ymd(d.getFullYear(), d.getMonth() + 1, d.getDate());
  }
  function ymd(y, m, d){ return `${y}-${String(m).padStart(2, '0')}-${String(d).padStart(2, '0')}`; }
  function daysInMonth(y, m){ return new Date(Date.UTC(y, m, 0)).getUTCDate(); }
  function addDay(date){
    const [y, m, d] = date.split('-').map(Number);
    const next = new Date(Date.UTC(y, m - 1, d + 1));
    return ymd(next.getUTCFullYear(), next.getUTCMonth() + 1, next.getUTCDate());
  }
  const datePart = v => String(v || '').slice(0, 10);
  const timePart = v => String(v || '').slice(11, 16);
  const minutesOf = time => { const [h, m] = time.split(':').map(Number); return h * 60 + m; };
  const isLocked = row => datePart(row.fromDateTime) < today;

  // the OT window of two HH:mm times: a To time earlier than the From time ends on the next day
  function span(from, to){
    if (!from || !to) return null;
    const diff = minutesOf(to) - minutesOf(from);
    if (diff === 0) return null;
    return { minutes: diff > 0 ? diff : diff + 1440, nextDay: diff < 0 };
  }
  // minutes between two saved yyyy-MM-ddTHH:mm stamps
  function stampMinutes(from, to){
    const ms = v => Date.UTC(+v.slice(0, 4), +v.slice(5, 7) - 1, +v.slice(8, 10), +v.slice(11, 13), +v.slice(14, 16));
    return Math.round((ms(to) - ms(from)) / 60000);
  }
  const hoursText = minutes => String(Math.round(minutes / 60 * 100) / 100);
  // the OT Hrs saved on the assignment (ot_assignments.OtHours), as the reports and the Pay Bill use them
  const savedHours = row => String(Number(row.otHours ?? stampMinutes(row.fromDateTime, row.toDateTime) / 60));
  function displayDate(date){
    const [y, m, d] = date.split('-').map(Number);
    if (!y) return '—';
    return new Date(y, m - 1, d).toLocaleDateString(document.documentElement.lang || undefined, { day: '2-digit', month: 'short', year: 'numeric' });
  }
  function toText(row){
    const nextDay = datePart(row.toDateTime) > datePart(row.fromDateTime);
    return timePart(row.toDateTime) + (nextDay ? ` (${t('OTNextDay')})` : '');
  }

  function actionButtons(row){
    const locked = isLocked(row);
    return [['view', 'fa-eye', t('View'), ''], ['edit', 'fa-pen', locked ? t('ValOtPastLocked') : t('Edit'), ''], ['delete', 'fa-trash', t('Delete'), 'color:var(--danger)']]
      .map(([act, icon, label, style]) => `<button type="button" class="btn btn-ghost btn-sm btn-icon" data-act="${act}" data-id="${esc(row.id)}"
          title="${esc(label)}" aria-label="${esc(label)}" style="${style}"${act === 'edit' && locked ? ' disabled' : ''}><i class="fa-solid ${icon}"></i></button>`)
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
      <td style="white-space:nowrap">${esc(displayDate(datePart(r.fromDateTime)))}</td>
      <td style="white-space:nowrap">${esc(timePart(r.fromDateTime))}</td>
      <td style="white-space:nowrap">${esc(toText(r))}</td>
      <td>${esc(savedHours(r))}</td>
      <td style="white-space:nowrap">${actionButtons(r)}</td>
    </tr>`).join('') : `<tr><td colspan="9" class="empty-state" style="text-align:center">${esc(t('NoOTAssignments'))}</td></tr>`;
  }

  // ── Calendar: the current month only, days before today disabled, no navigation ──
  function renderCalendar(){
    const [y, m] = today.split('-').map(Number);
    const lang = document.documentElement.lang || undefined;
    const title = new Date(y, m - 1, 1).toLocaleDateString(lang, { month: 'long', year: 'numeric' });
    // weekday names starting from Sunday (4 Jan 2026 is a Sunday)
    const dows = Array.from({ length: 7 }, (_, i) => new Date(2026, 0, 4 + i).toLocaleDateString(lang, { weekday: 'short' }));
    const lead = new Date(y, m - 1, 1).getDay();
    const days = [];
    for (let d = 1; d <= daysInMonth(y, m); d++){
      const date = ymd(y, m, d);
      const cls = ['ot-cal-day', date === today ? 'today' : '', date === selectedDate ? 'selected' : ''].filter(Boolean).join(' ');
      days.push(`<button type="button" class="${cls}" data-date="${date}" aria-label="${esc(displayDate(date))}"
        aria-pressed="${date === selectedDate}"${date < today ? ' disabled' : ''}>${d}</button>`);
    }
    $('otCalendar').innerHTML = `<div class="ot-cal-title">${esc(title)}</div>
      <div class="ot-cal-grid">
        ${dows.map(w => `<div class="ot-cal-dow" aria-hidden="true">${esc(w)}</div>`).join('')}
        ${'<div></div>'.repeat(lead)}${days.join('')}
      </div>`;
  }

  function showHours(){
    const s = span($('otFrom').value, $('otTo').value);
    $('otHours').innerHTML = s ? `${esc(hoursText(s.minutes))}${s.nextDay ? ` <small>${esc(t('OTNextDay'))}</small>` : ''}` : '—';
  }

  // ── Add / edit form ───────────────────────────────────────────
  function clearErrors(){
    ['request', 'staff_id', 'ot_date', 'from_datetime', 'to_datetime'].forEach(k => {
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
    selectedDate = row ? datePart(row.fromDateTime) : today;
    $('otFrom').value = row ? timePart(row.fromDateTime) : '';
    $('otTo').value = row ? timePart(row.toDateTime) : '';
    currentShift = row ? { staffName: row.staffName, shiftName: row.shiftName, shiftStart: row.shiftStart, shiftEnd: row.shiftEnd } : null;
    renderShift();
    renderCalendar();
    showHours();
    openModal('otFormModal');
  }

  // ── The selected employee's shift ─────────────────────────────
  // { staffName, shiftName, shiftStart, shiftEnd } of the employee in the form (times HH:mm, null when unknown)
  let currentShift = null;

  function clock(hhmm){
    const [h, m] = hhmm.split(':').map(Number);
    return new Date(2000, 0, 1, h, m).toLocaleTimeString(document.documentElement.lang || undefined, { hour: 'numeric', minute: '2-digit' });
  }

  // same as OtAssignmentMapper.OtWindow: from the end of the shift that ends on the OT date until the next shift starts,
  // in minutes from the start of the OT date
  function otWindow(shift){
    if (!shift || !shift.shiftStart || !shift.shiftEnd) return null;
    const from = minutesOf(shift.shiftEnd);
    let until = minutesOf(shift.shiftStart);
    if (until <= from) until += 1440;
    return { from, until };
  }

  function renderShift(){
    const box = $('otShiftInfo');
    if (!currentShift){ box.hidden = true; box.innerHTML = ''; return; }
    const s = currentShift;
    const win = otWindow(s);
    box.hidden = false;
    box.classList.toggle('warn', !win);
    if (!s.shiftName){
      box.innerHTML = `<div class="ot-shift-msg"><i class="fa-solid fa-triangle-exclamation"></i>${esc(t('OtNoShift'))}</div>`;
      return;
    }
    const alertText = esc(t('OtShiftAlert', s.staffName, s.shiftName));
    if (!win){
      box.innerHTML = `<div class="ot-shift-msg"><i class="fa-solid fa-triangle-exclamation"></i>${alertText}</div>
        <div>${esc(t('OtShiftTimesUnknown'))}</div>`;
      return;
    }
    const until = clock(s.shiftStart) + (win.until >= 1440 ? ` (${t('OTNextDay')})` : '');
    box.innerHTML = `<div class="ot-shift-msg"><i class="fa-solid fa-circle-info"></i>${alertText}</div>
      <div class="ot-shift-grid">
        <div><span>${esc(t('Shift'))}</span><strong>${esc(s.shiftName)}</strong></div>
        <div><span>${esc(t('ShiftTimings'))}</span><strong>${esc(clock(s.shiftStart))} – ${esc(clock(s.shiftEnd))}</strong></div>
      </div>
      <div style="margin-top:8px">${esc(t('OtAllowedWindow', clock(s.shiftEnd), until))}</div>`;
  }

  function selectStaff(){
    const option = staff.find(s => s.id === $('otStaff').value);
    currentShift = option ? { staffName: option.fullName, shiftName: option.shiftName, shiftStart: option.shiftStart, shiftEnd: option.shiftEnd } : null;
    renderShift();
    // start the OT where the shift ends, unless a time is already entered
    if (currentShift?.shiftEnd && !$('otFrom').value){ $('otFrom').value = currentShift.shiftEnd; showHours(); }
  }

  // same rules as OtAssignmentMapper.ValidateTimes and ShiftConflict on the server
  function validate(staffId, from, to){
    const errors = {};
    if (!editingId && !staffId) errors.staff_id = t('ValOtSelectStaff');
    if (!selectedDate) errors.ot_date = t('ValOtSelectDate');
    else if (selectedDate < today) errors.ot_date = t('ValOtDatePast');
    if (!from) errors.from_datetime = t('ValOtTimes');
    else if (!to) errors.to_datetime = t('ValOtTimes');
    else if (from === to) errors.to_datetime = t('ValOtToAfterFrom');
    else {
      // OT may not fall inside the regular shift: it starts once the shift ends and finishes by the next shift's start
      const win = otWindow(currentShift);
      const start = minutesOf(from), end = start + span(from, to).minutes;
      if (win && start < win.from) errors.from_datetime = t('ValOtBeforeShiftEnd');
      else if (win && end > win.until) errors.to_datetime = t('ValOtIntoNextShift');
    }
    return errors;
  }

  async function save(){
    if (saving) return;
    clearErrors();
    const staffId = $('otStaff').value || null, from = $('otFrom').value, to = $('otTo').value;
    const errors = validate(staffId, from, to);
    if (Object.keys(errors).length){ showErrors(errors); return; }
    const s = span(from, to);
    const body = { staffId, fromDateTime: `${selectedDate}T${from}`, toDateTime: `${s.nextDay ? addDay(selectedDate) : selectedDate}T${to}` };

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
      ['OTDate', displayDate(datePart(row.fromDateTime))], ['OTHours', savedHours(row)],
      ['OTFromTime', timePart(row.fromDateTime)], ['OTToTime', toText(row)],
      ['Shift', row.shiftName], ['ShiftTimings', row.shiftStart && row.shiftEnd ? `${clock(row.shiftStart)} – ${clock(row.shiftEnd)}` : ''],
      ['UpdatedBy', row.by]
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
    $('otStaff').addEventListener('change', selectStaff);
    $('otFrom').addEventListener('input', showHours);
    $('otTo').addEventListener('input', showHours);
    $('otCalendar').addEventListener('click', e => {
      const day = e.target.closest('[data-date]');
      if (!day || day.disabled) return;
      selectedDate = day.dataset.date;
      renderCalendar();
      $('otCalendar').querySelector(`[data-date="${selectedDate}"]`)?.focus();
    });
    $('otBody').addEventListener('click', e => {
      const btn = e.target.closest('[data-act]');
      const row = btn && rows.find(r => r.id === btn.dataset.id);
      if (!row) return;
      if (btn.dataset.act === 'view') view(row);
      else if (btn.dataset.act === 'edit' && !isLocked(row)) openForm(row);
      else if (btn.dataset.act === 'delete') remove(row);
    });
    load();
  });
})();
