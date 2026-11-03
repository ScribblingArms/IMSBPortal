(function AbstractReport(){
  const I18N = window.ReportsI18n || {};
  const $ = id => document.getElementById(id);
  const t = key => I18N[key] || key;
  const esc = value => String(value == null ? '' : value)
    .replace(/[&<>"']/g, character => ({ '&':'&amp;', '<':'&lt;', '>':'&gt;', '"':'&quot;', "'":'&#39;' }[character]));
  const qs = values => Object.entries(values)
    .filter(([, value]) => value !== '' && value != null)
    .map(([key, value]) => `${key}=${encodeURIComponent(value)}`).join('&');
  let defaultDate = '';
  let report = null, selectedSection = null, selectedMetric = null, requestSequence = 0;

  async function load(){
    const date = $('abstractDate').value;
    if (!date){
      requestSequence++;
      $('abstractError').textContent = t('SelectDate');
      $('abstractError').style.display = 'block';
      clearReport();
      return;
    }

    const sequence = ++requestSequence;
    const error = $('abstractError');
    error.textContent = '';
    error.style.display = '';
    clearReport();
    try {
      const response = await fetch('/Reports/AbstractData?' + qs({ date }));
      if (sequence !== requestSequence) return;
      if (!response.ok){
        const json = response.status === 400 ? await response.json().catch(() => ({})) : {};
        if (sequence !== requestSequence) return;
        error.textContent = Object.values(json.errors || {})[0] || `${t('CouldNotReachServer')} (${response.status})`;
        error.style.display = 'block';
        clearReport();
        return;
      }
      report = await response.json();
      if (sequence !== requestSequence) return;
      selectedSection = null;
      selectedMetric = null;
      $('abstractReportCount').textContent = String(report.employees.length);
      renderSummary();
      renderDetails();
    } catch (exception) {
      if (sequence !== requestSequence) return;
      console.error(exception);
      error.textContent = t('CouldNotReachServer');
      error.style.display = 'block';
      clearReport();
    }
  }

  function clearReport(){
    report = null;
    selectedSection = null;
    selectedMetric = null;
    $('abstractReportCount').textContent = '0';
    $('abstractSummaryBody').innerHTML = '';
    $('abstractExportBtn').hidden = true;
    renderDetails();
  }

  function renderSummary(){
    $('abstractSummaryBody').innerHTML = report.sections.length ? report.sections.map((section, index) => `<tr>
      <td>${index + 1}</td>
      <td>${esc(section.name)}</td>
      ${countCell(section, 'total', section.total, '', section.key === selectedSection && selectedMetric === 'total')}
      ${countCell(section, 'attended', section.attended, 'attended', section.key === selectedSection && selectedMetric === 'attended')}
      ${countCell(section, 'notAttended', section.notAttended, 'not-attended', section.key === selectedSection && selectedMetric === 'notAttended')}
    </tr>`).join('') : `<tr><td colspan="5" class="empty-state">${esc(t('NoEmployeesForSection'))}</td></tr>`;
  }

  function countCell(section, metric, value, cssClass, selected){
    if (value == null) return `<td><span title="${esc(t('ArtisansAttendanceUnavailable'))}">—</span></td>`;
    return `<td><button type="button" class="abstract-count-link ${cssClass || ''}" data-section="${esc(section.key)}" data-metric="${metric}" aria-pressed="${selected}">${value}</button></td>`;
  }

  function renderDetails(){
    const panel = $('abstractDetailsPanel');
    const visible = Boolean(report && selectedSection && selectedMetric);
    panel.hidden = !visible;
    if (!visible) return;

    const rows = selectedEmployees();
    $('abstractExportBtn').hidden = rows.length === 0;
    $('abstractDetailCount').textContent = String(rows.length);
    const section = report.sections.find(item => item.key === selectedSection);
    const metricName = selectedMetric === 'attended' ? t('Attended')
      : selectedMetric === 'notAttended' ? t('NotAttended') : t('StaffCountTotal');
    $('abstractDetailTitle').textContent = `${t('EmployeeDetails')} — ${section ? section.name : ''} — ${metricName}`;
    $('abstractDetailsBody').innerHTML = rows.length ? rows.map((employee, index) => `<tr>
      <td>${index + 1}</td>
      <td><div class="abstract-detail-staff-name">${esc(employee.name)}</div><div class="abstract-detail-employee-id">${esc(employee.employeeId)}</div></td>
      <td>${esc(employee.designation || employee.employeeType || '—')}</td>
      <td>${esc(statusLabel(employee.attendanceStatus))}</td>
    </tr>`).join('') : `<tr><td colspan="4" class="empty-state">${esc(t('NoEmployeesForSection'))}</td></tr>`;
  }

  function selectedEmployees(){
    if (!report || !selectedSection || !selectedMetric) return [];
    return report.employees.filter(employee => {
      if (employee.sectionKey !== selectedSection) return false;
      if (selectedMetric === 'attended') return employee.attended === true;
      if (selectedMetric === 'notAttended') return employee.attended === false;
      return true;
    });
  }

  function statusLabel(status){
    const labels = {
      PRESENT: t('Present'),
      ABSENT: t('Absent'),
      LEAVE: t('Leave'),
      HALF_DAY: t('HalfDay'),
      NOT_MARKED: t('NotMarked')
    };
    return labels[status] || '—';
  }

  document.addEventListener('DOMContentLoaded', () => {
    defaultDate = $('abstractDate').value;
    $('abstractDate').addEventListener('change', load);
    $('abstractLoadBtn').addEventListener('click', load);
    $('abstractResetBtn').addEventListener('click', () => {
      $('abstractDate').value = defaultDate;
      load();
    });
    $('abstractSummaryBody').addEventListener('click', event => {
      const button = event.target.closest('[data-section]');
      if (!button) return;
      selectedSection = button.dataset.section;
      selectedMetric = button.dataset.metric;
      renderSummary();
      renderDetails();
    });
    $('abstractExportBtn').addEventListener('click', () => {
      if (!report || $('abstractExportBtn').hidden || selectedEmployees().length === 0) return;
      window.location.href = '/Reports/AbstractExport?' + qs({
        date: report.date,
        section: selectedSection,
        metric: selectedMetric
      });
    });
    load();
  });
})();
