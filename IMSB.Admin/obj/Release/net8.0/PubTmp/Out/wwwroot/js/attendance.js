// Attendance page: the day's Attendance Entry sheet (view-only), department-wise cards and the Attendance History
// list with view / edit / delete, through the /Attendance endpoints. Localized strings come from window.AttendanceI18n
// (rendered by Views/Attendance/Index.cshtml).
(function AttendanceModule(){
  let day = isoOf(new Date());  // selected date (yyyy-MM-dd)
  let data = null;              // /Attendance/Data for that day
  let historyRows = [];
  let editingId = null;
  let saving = false, loadSeq = 0, histSeq = 0, histTimer = null;

  // ── i18n / formatting ─────────────────────────────────────────
  const I18N = window.AttendanceI18n || {};
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
  const toast = (title, msg, type) => { if (typeof showToast === 'function') showToast(esc(title), esc(msg), type); };
  const token = () => document.querySelector('#attendanceForm input[name="__RequestVerificationToken"]')?.value || '';
  const qs = obj => Object.entries(obj).filter(([, v]) => v !== '' && v != null).map(([k, v]) => `${k}=${encodeURIComponent(v)}`).join('&');
  function isoOf(d){ return `${d.getFullYear()}-${String(d.getMonth()+1).padStart(2,'0')}-${String(d.getDate()).padStart(2,'0')}`; }
  const todayIso = () => isoOf(new Date());
  function parseIso(iso){
    const m = /^(\d{4})-(\d{2})-(\d{2})$/.exec(iso || '');
    return m ? new Date(+m[1], +m[2] - 1, +m[3]) : null;
  }
  function addDays(iso, n){ const d = parseIso(iso); d.setDate(d.getDate() + n); return isoOf(d); }
  function formatDate(iso){
    const d = parseIso(iso);
    return d ? d.toLocaleDateString(lang(), { day: '2-digit', month: 'short', year: 'numeric' }) : (iso || '—');
  }
  function longDate(iso){
    const d = parseIso(iso);
    return d ? d.toLocaleDateString(lang(), { weekday: 'long', day: 'numeric', month: 'long', year: 'numeric' }) : '';
  }
  const hours = v => String(Math.round(Number(v || 0) * 100) / 100);
  const minutes = hhmm => { const m = /^(\d{2}):(\d{2})$/.exec(hhmm || ''); return m ? +m[1] * 60 + +m[2] : null; };

  const STATUS = {
    PRESENT:  { key: 'Present', badge: 'badge-success', dot: 'present', icon: 'fa-check' },
    ABSENT:   { key: 'Absent',  badge: 'badge-danger',  dot: 'absent',  icon: 'fa-xmark' },
    LEAVE:    { key: 'Leave',   badge: 'badge-warning', dot: 'leave',   icon: 'fa-umbrella-beach' },
    HALF_DAY: { key: 'HalfDay', badge: 'badge-halfday', dot: 'halfday', icon: 'fa-circle-half-stroke' }
  };
  const worked = s => s === 'PRESENT' || s === 'HALF_DAY';
  const statusLabel = s => STATUS[s] ? t(STATUS[s].key) : t('NotMarked');
  const statusBadge = s => `<span class="badge ${STATUS[s]?.badge || 'badge-neutral'}">${esc(statusLabel(s))}</span>`;
  const attendanceStatusBadge = (status, leaveWithoutPay) => statusBadge(status)
    + (leaveWithoutPay ? ` <span class="badge badge-danger">${esc(t('LeaveWithoutPay'))}</span>` : '');
  const leaveBalanceChannel = 'BroadcastChannel' in window ? new BroadcastChannel('imsb-leave-balances') : null;
  const notifyAttendanceChanged = () => leaveBalanceChannel?.postMessage('attendance-saved');

  // ── Load the selected day ─────────────────────────────────────
  async function load(){
    const seq = ++loadSeq;
    try {
      const res = await fetch('/Attendance/Data?' + qs({ date: day }));
      if (!res.ok){ console.warn('Attendance: data request failed', res.status); toast(t('Error'), t('CouldNotReachServer'), 'error'); return; }
      const json = await res.json();
      if (seq !== loadSeq) return;
      data = json;
      renderStats();
      renderWeek();
      renderSheet();
      renderDepartments();
    } catch (e) {
      console.error(e);
      toast(t('Error'), t('CouldNotReachServer'), 'error');
    }
  }

  function goTo(iso){
    if (!iso) return;
    if (iso > todayIso()) iso = todayIso();
    if (iso === day && data){ $('attDate').value = day; return; }
    day = iso;
    load();
  }

  function renderStats(){
    const s = data.stats;
    const set = (id, v) => { const el = $(id); if (el) el.textContent = v; };
    set('statTotal', s.total);
    set('statPresent', s.present);
    set('statAbsent', s.absent);
    set('statLeave', s.leave);
    set('statHalfDay', s.halfDay);
    set('statOvertime', s.withOvertime);
    set('statNotMarked', t('NotMarkedCount', s.notMarked));
    set('statPercent', s.percent == null ? '—' : `${t('AttendancePercent')}: ${s.percent}%`);
    set('entryDateLabel', longDate(data.date));
    $('attDate').value = data.date;
    $('attDate').max = data.today;
    $('futureNote').style.display = data.isFuture ? '' : 'none';
  }

  function renderWeek(){
    const w = data.week;
    $('weekLabel').textContent = t('WeekOf', `${formatDate(w.start)} – ${formatDate(w.end)}`);
    $('weekNext').disabled = w.end >= data.today;
    $('weekCalendar').innerHTML = w.days.map(d => {
      const dt = parseIso(d.date);
      return `<div class="week-day${d.date === data.date ? ' active' : ''}${d.future ? ' future' : ''}" data-date="${d.date}"
          role="button" tabindex="${d.future ? -1 : 0}" aria-label="${esc(longDate(d.date))}" aria-disabled="${d.future}">
        <div class="wd-name">${esc(dt.toLocaleDateString(lang(), { weekday: 'short' }))}</div>
        <div class="wd-date">${dt.getDate()}</div>
        <div class="wd-pct">${d.percent == null ? '–' : d.percent + '%'}</div>
      </div>`;
    }).join('');
  }

  // ── Attendance Entry sheet (view-only) ────────────────────────
  const rowEls = () => [...document.querySelectorAll('#attendanceRows .att-row')];

  function actionButtons(id, withEdit){
    return [['view', 'fa-eye', t('View'), ''], withEdit ? ['edit', 'fa-pen', t('Edit'), ''] : null, ['delete', 'fa-trash', t('Delete'), 'color:var(--danger)']]
      .filter(Boolean)
      .map(([act, icon, label, style]) => `<button type="button" class="btn btn-ghost btn-sm btn-icon" data-act="${act}" data-id="${esc(id)}"
          title="${esc(label)}" aria-label="${esc(label)}" style="${style}"><i class="fa-solid ${icon}"></i></button>`)
      .join('');
  }

  function rowHtml(r, i){
    const rec = r.record, s = STATUS[rec?.status];
    return `<div class="att-entry-grid att-row" data-i="${i}">
      <div><div class="att-status-dot ${s ? s.dot : 'none'}"><i class="fa-solid ${s ? s.icon : 'fa-minus'}"></i></div></div>
      <div>
        <div class="att-name">${esc(r.name)}</div>
        <div class="att-sub">${esc(r.employeeId)} · ${esc(r.designation)}</div>
      </div>
      <div><span class="badge badge-info" style="font-size:.65rem">${esc(r.departmentName || '—')}</span><div class="att-sub">${esc(r.shift || '')}</div></div>
      <div class="att-in-text">${esc(rec?.inTime || '—')}</div>
      <div class="att-out-text">${esc(rec?.outTime || '—')}</div>
      <div class="att-hours">${rec?.workedHours > 0 ? hours(rec.workedHours) + 'h' : '—'}</div>
      <div class="att-ot">${rec?.overtimeHours > 0 ? `<span class="overtime-chip"><i class="fa-solid fa-clock"></i>${hours(rec.overtimeHours)}h</span>` : '—'}</div>
      <div>${attendanceStatusBadge(rec?.status, rec?.leaveWithoutPay)}</div>
      <div class="att-actions">${rec ? actionButtons(rec.id, false) : ''}</div>
    </div>`;
  }

  function renderSheet(){
    const box = $('attendanceRows');
    if (!data.rows.length){
      box.innerHTML = `<div class="att-empty">${esc(t('NoStaffForAttendance'))}</div>`;
    } else {
      box.innerHTML = data.rows.map(rowHtml).join('') + '<div class="att-empty" id="sheetEmpty" style="display:none"></div>';
      applySheetFilter();
    }
  }

  function applySheetFilter(){
    const dept = $('entryDept').value, q = $('entrySearch').value.trim().toLowerCase();
    let shown = 0;
    rowEls().forEach(el => {
      const r = data.rows[+el.dataset.i];
      const ok = (!dept || r.departmentId === dept) && (!q || `${r.name} ${r.employeeId} ${r.designation}`.toLowerCase().includes(q));
      el.classList.toggle('hidden', !ok);
      if (ok) shown++;
    });
    const empty = $('sheetEmpty');
    if (empty){
      empty.textContent = dept && !q ? t('NoStaffInDepartment') : t('NoStaffForAttendance');
      empty.style.display = shown ? 'none' : '';
    }
  }

  function showError(el, msg){
    if (!el) return;
    el.textContent = msg;
    const group = el.closest('.form-group');
    if (group) group.classList.add('has-error'); else el.style.display = 'block';
  }

  // ── Department-wise ───────────────────────────────────────────
  function renderDepartments(){
    const box = $('deptAttCards');
    const list = data.departments.filter(d => d.total > 0);
    if (!list.length){ box.innerHTML = `<div class="card"><div class="card-body att-empty">${esc(t('NoStaffForAttendance'))}</div></div>`; return; }
    box.innerHTML = list.map(d => {
      const pct = d.percent;
      const badge = pct == null ? 'badge-neutral' : pct >= 90 ? 'badge-success' : pct >= 75 ? 'badge-warning' : 'badge-danger';
      const color = pct == null ? 'var(--border)' : pct >= 90 ? 'var(--success)' : pct >= 75 ? 'var(--warning)' : 'var(--danger)';
      const count = (n, status, c) => `<div><div class="n" style="color:${c}">${n}</div><div class="l">${esc(statusLabel(status))}</div></div>`;
      return `<div class="card">
        <div class="card-header">
          <div class="card-title"><i class="fa-solid fa-building"></i> ${esc(d.name)}</div>
          <span class="badge ${badge}">${pct == null ? '—' : pct + '%'}</span>
        </div>
        <div class="card-body">
          <div class="att-dept-counts">
            ${count(d.present, 'PRESENT', 'var(--success)')}${count(d.absent, 'ABSENT', 'var(--danger)')}${count(d.leave, 'LEAVE', 'var(--warning)')}${count(d.halfDay, 'HALF_DAY', '#0d9488')}
          </div>
          <div class="att-bar"><div style="width:${pct || 0}%;background:${color}"></div></div>
          <div class="att-dept-foot">
            <span>${esc(t('AttendanceRatio', d.present + d.halfDay, d.total))}</span>
            <span>${esc(t('NotMarkedCount', d.notMarked))}</span>
            <span>${esc(t('MonthToDate', d.monthPercent == null ? '—' : d.monthPercent + '%'))}</span>
          </div>
          <button type="button" class="btn btn-outline btn-sm" style="margin-top:12px" data-dept="${esc(d.id)}"><i class="fa-solid fa-fingerprint"></i> ${esc(t('OpenInEntry'))}</button>
        </div>
      </div>`;
    }).join('');
  }

  // ── Attendance History ────────────────────────────────────────
  function histFilters(){
    return { q: $('histSearch').value.trim(), month: $('histMonth').value, departmentId: $('histDept').value, status: $('histStatus').value };
  }

  async function loadHistory(){
    const seq = ++histSeq;
    const err = $('hist_err');
    err.textContent = ''; err.style.display = '';
    try {
      const res = await fetch('/Attendance/History?' + qs(histFilters()));
      if (seq !== histSeq) return;
      if (res.status === 400){
        const json = await res.json().catch(() => ({}));
        err.textContent = Object.values(json.errors || {})[0] || t('CheckForm'); err.style.display = 'block';
        return;
      }
      if (!res.ok){ console.warn('Attendance: history request failed', res.status); return; }
      const json = await res.json();
      if (seq !== histSeq) return;
      historyRows = json.rows || [];
      renderHistory(json.totals);
    } catch (e) {
      console.error(e);
      toast(t('Error'), t('CouldNotReachServer'), 'error');
    }
  }

  function renderHistory(totals){
    $('histTotals').textContent = t('HistoryTotals', totals.records, totals.present, totals.absent, totals.leave, totals.halfDay, hours(totals.hours), hours(totals.overtime));
    const body = $('historyBody');
    if (!historyRows.length){ body.innerHTML = `<tr><td colspan="9" class="empty-state">${esc(t('NoAttendanceRecords'))}</td></tr>`; return; }
    body.innerHTML = historyRows.map(r => `<tr>
      <td><strong>${esc(r.name)}</strong><div class="att-sub">${esc(r.employeeId)}</div></td>
      <td>${esc(r.departmentName || '—')}</td>
      <td>${esc(formatDate(r.date))}</td>
      <td>${esc(r.inTime || '—')}</td>
      <td>${esc(r.outTime || '—')}</td>
      <td>${r.workedHours > 0 ? hours(r.workedHours) + 'h' : '—'}</td>
      <td>${r.overtimeHours > 0 ? `<span class="overtime-chip">${hours(r.overtimeHours)}h</span>` : '—'}</td>
      <td>${attendanceStatusBadge(r.status, r.leaveWithoutPay)}</td>
      <td style="white-space:nowrap">${actionButtons(r.id, true)}</td>
    </tr>`).join('');
  }

  function resetHistory(){
    $('histSearch').value = '';
    $('histMonth').value = todayIso().slice(0, 7);
    $('histDept').value = '';
    $('histStatus').value = '';
    loadHistory();
  }

  // ── Single records: view / edit / delete ──────────────────────
  async function refreshAfterChange(){
    await Promise.all([load(), loadHistory()]);
  }

  async function getRecord(id){
    try {
      const res = await fetch('/Attendance/Get/' + encodeURIComponent(id));
      if (res.status === 404){ toast(t('Error'), t('AttendanceNotFound'), 'error'); await refreshAfterChange(); return null; }
      if (!res.ok){ toast(t('Error'), `${t('CheckForm')} (${res.status})`, 'error'); return null; }
      return await res.json();
    } catch (e) {
      console.error(e);
      toast(t('Error'), t('CouldNotReachServer'), 'error');
      return null;
    }
  }

  async function viewRecord(id){
    const r = await getRecord(id);
    if (!r) return;
    const item = (label, html) => `<dt>${esc(label)}</dt><dd>${html}</dd>`;
    $('attViewTitle').textContent = `${r.name} · ${formatDate(r.date)}`;
    $('attViewBody').innerHTML = `<dl class="att-detail">
      ${item(t('EmployeeID'), esc(r.employeeId))}
      ${item(t('Department'), esc(r.departmentName || '—'))}
      ${item(t('Designation'), esc(r.designation || '—'))}
      ${item(t('Date'), esc(longDate(r.date)))}
      ${item(t('Status'), attendanceStatusBadge(r.status, r.leaveWithoutPay))}
      ${item(t('InTime'), esc(r.inTime || '—'))}
      ${item(t('OutTime'), esc(r.outTime || '—'))}
      ${item(t('Hours'), r.workedHours > 0 ? hours(r.workedHours) + 'h' : '—')}
      ${item(t('OTHours'), r.overtimeHours > 0 ? hours(r.overtimeHours) + 'h' : '—')}
      ${item(t('Remarks'), esc(r.remarks || '—'))}
      ${item(t('UpdatedBy'), esc(r.by || '—'))}
    </dl>`;
    if (typeof openModal === 'function') openModal('attViewModal');
  }

  function clearEditErrors(){
    ['status', 'in_time', 'out_time', 'remarks'].forEach(k => {
      const el = $('err_edit_' + k);
      el.textContent = '';
      el.closest('.form-group')?.classList.remove('has-error');
    });
  }
  function toggleEditTimes(){
    const on = worked($('editStatus').value);
    ['editIn', 'editOut'].forEach(id => { $(id).disabled = !on; });
  }

  async function editRecord(id){
    const r = await getRecord(id);
    if (!r) return;
    editingId = id;
    clearEditErrors();
    $('editStaff').textContent = `${r.name} (${r.employeeId})`;
    $('editDate').textContent = longDate(r.date);
    $('editStatus').value = r.status;
    $('editIn').value = r.inTime || '';
    $('editOut').value = r.outTime || '';
    $('editRemarks').value = r.remarks || '';
    toggleEditTimes();
    if (typeof openModal === 'function') openModal('attEditModal');
  }

  async function saveEdit(){
    if (!editingId || saving) return;
    clearEditErrors();
    const status = $('editStatus').value;
    const body = {
      status,
      inTime: worked(status) ? ($('editIn').value || null) : null,
      outTime: worked(status) ? ($('editOut').value || null) : null,
      remarks: $('editRemarks').value.trim() || null
    };
    const errors = {};
    if (worked(status)){
      if (!body.inTime) errors.in_time = t('ValInTimeRequired');
      else if (body.outTime && minutes(body.outTime) <= minutes(body.inTime)) errors.out_time = t('ValOutBeforeIn');
    }
    if (body.remarks && body.remarks.length > 500) errors.remarks = t('ValRemarks');
    const show = errs => Object.entries(errs).forEach(([k, msg]) => showError($('err_edit_' + k), msg));
    if (Object.keys(errors).length){ show(errors); return; }

    saving = true;
    $('editSaveBtn').disabled = true;
    try {
      const res = await fetch('/Attendance/Update/' + encodeURIComponent(editingId), {
        method: 'POST',
        headers: { 'Content-Type': 'application/json', 'RequestVerificationToken': token() },
        body: JSON.stringify(body)
      });
      if (res.status === 400){
        const json = await res.json().catch(() => ({}));
        show(json.errors || {});
        toast(t('Error'), t('CheckForm'), 'error');
        return;
      }
      if (res.status === 404){ toast(t('Error'), t('AttendanceNotFound'), 'error'); closeModal('attEditModal'); await refreshAfterChange(); return; }
      if (!res.ok){ toast(t('Error'), `${t('CheckForm')} (${res.status})`, 'error'); return; }
      const r = await res.json();
      notifyAttendanceChanged();
      const lopMessage = r.leaveWithoutPay ? ` ${t('AttendanceLopMessage', 1)}` : '';
      toast(t('Success'), t('AttendanceUpdated', r.name, formatDate(r.date)) + lopMessage, 'success');
      closeModal('attEditModal');
      editingId = null;
      await refreshAfterChange();
    } catch (e) {
      console.error(e);
      toast(t('Error'), t('CouldNotReachServer'), 'error');
    } finally {
      saving = false;
      $('editSaveBtn').disabled = false;
    }
  }

  async function deleteRecord(id){
    const r = await getRecord(id);
    if (!r) return;
    if (!confirm(t('ConfirmDeleteAttendance', r.name, formatDate(r.date)))) return;
    try {
      const res = await fetch('/Attendance/Delete/' + encodeURIComponent(id), { method: 'POST', headers: { 'RequestVerificationToken': token() } });
      if (res.status === 404){ toast(t('Error'), t('AttendanceNotFound'), 'error'); await refreshAfterChange(); return; }
      if (!res.ok){ toast(t('Error'), `${t('CheckForm')} (${res.status})`, 'error'); return; }
      notifyAttendanceChanged();
      toast(t('Success'), t('AttendanceDeleted', r.name, formatDate(r.date)), 'success');
      await refreshAfterChange();
    } catch (e) {
      console.error(e);
      toast(t('Error'), t('CouldNotReachServer'), 'error');
    }
  }

  function onAction(e){
    const btn = e.target.closest('[data-act]');
    if (!btn) return;
    const id = btn.dataset.id;
    if (btn.dataset.act === 'view') viewRecord(id);
    else if (btn.dataset.act === 'edit') editRecord(id);
    else if (btn.dataset.act === 'delete') deleteRecord(id);
  }

  // ── Wiring ────────────────────────────────────────────────────
  document.addEventListener('DOMContentLoaded', () => {
    if (!$('attendanceRows')) return;

    $('attendanceForm').addEventListener('submit', e => e.preventDefault());
    $('attDate').addEventListener('change', () => goTo($('attDate').value));
    $('weekPrev').addEventListener('click', () => goTo(addDays(day, -7)));
    $('weekNext').addEventListener('click', () => goTo(addDays(day, 7)));
    $('todayBtn').addEventListener('click', () => goTo(todayIso()));
    const pickDay = e => {
      const el = e.target.closest('.week-day');
      if (el && !el.classList.contains('future')) goTo(el.dataset.date);
    };
    $('weekCalendar').addEventListener('click', pickDay);
    $('weekCalendar').addEventListener('keydown', e => { if (e.key === 'Enter' || e.key === ' '){ e.preventDefault(); pickDay(e); } });

    $('entryDept').addEventListener('change', applySheetFilter);
    $('entrySearch').addEventListener('input', applySheetFilter);
    $('attendanceRows').addEventListener('click', onAction);

    $('deptAttCards').addEventListener('click', e => {
      const btn = e.target.closest('[data-dept]');
      if (!btn) return;
      $('entryDept').value = btn.dataset.dept;
      $('entrySearch').value = '';
      applySheetFilter();
      document.querySelector('#attendanceTabs [data-tab="entry"]')?.click();
    });

    $('histMonth').value = todayIso().slice(0, 7);
    $('histMonth').max = todayIso().slice(0, 7);
    $('histSearch').addEventListener('input', () => { clearTimeout(histTimer); histTimer = setTimeout(loadHistory, 300); });
    ['histMonth', 'histDept', 'histStatus'].forEach(id => $(id).addEventListener('change', loadHistory));
    $('histResetBtn').addEventListener('click', resetHistory);
    $('histExportBtn').addEventListener('click', () => { window.location.href = '/Attendance/Export?' + qs(histFilters()); });
    $('historyBody').addEventListener('click', onAction);

    $('editStatus').addEventListener('change', toggleEditTimes);
    $('editSaveBtn').addEventListener('click', saveEdit);
    $('attEditForm').addEventListener('submit', e => { e.preventDefault(); saveEdit(); });


    load();
    loadHistory();
  });
})();
