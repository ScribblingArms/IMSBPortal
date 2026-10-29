(function () {
  const $ = id => document.getElementById(id);
  const I = window.PayBillI18n || {};
  const t = key => I[key] || key;
  const esc = value => String(value == null ? '' : value).replace(/[&<>"']/g, c =>
    ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]));
  const qs = values => Object.entries(values).map(([key, value]) => `${key}=${encodeURIComponent(value)}`).join('&');
  const money = value => '₹' + Number(value || 0).toLocaleString('en-IN', { minimumFractionDigits: 2, maximumFractionDigits: 2 });
  const token = () => document.querySelector('input[name="__RequestVerificationToken"]')?.value || '';
  let currentRows = [];
  let isGenerated = false;
  let hasUnsavedChanges = false;

  function period() {
    return { year: $('payBillYear').value, month: $('payBillMonth').value };
  }

  function staffFilter() {
    return $('payBillStaffFilter').value;
  }

  function visibleRows() {
    const employeeId = staffFilter();
    return employeeId ? currentRows.filter(row => row.employeeId === employeeId) : currentRows;
  }

  function setError(message) {
    const error = $('payBillError');
    error.textContent = message || '';
    error.style.display = message ? 'block' : 'none';
  }

  async function load() {
    setError('');
    try {
      const response = await fetch('/Payroll/Data?' + qs(period()));
      const data = await response.json();
      if (!response.ok) {
        setError(Object.values(data.errors || {})[0] || t('PayrollLoadFailed'));
        return;
      }
      currentRows = data.rows || [];
      isGenerated = data.isGenerated;
      hasUnsavedChanges = false;
      $('payBillPeriod').textContent = `${new Date(+data.year, +data.month - 1, 1).toLocaleDateString(document.documentElement.lang, { month: 'long', year: 'numeric' })} · ${data.monthDays} days${data.isGenerated ? '' : ' · ' + t('Draft')}`;
      render();
    } catch (error) {
      console.error(error);
      setError(t('CouldNotReachServer'));
    }
  }

  function input(row, key) {
    return `<input class="form-control pay-input" type="number" min="0" step="0.01" value="${Number(row[key] || 0).toFixed(2)}" data-staff="${esc(row.staffId)}" data-key="${key}" aria-label="${esc(t(key === 'allowances' ? 'Allowances' : key === 'overtimeAmount' ? 'OvertimeAmount' : 'Deductions'))}">`;
  }

  function render() {
    const staffFilterSelect = $('payBillStaffFilter');
    const selectedStaff = staffFilterSelect.value;
    staffFilterSelect.innerHTML = `<option value="">${esc(t('AllStaff'))}</option>` + currentRows.map(row =>
      `<option value="${esc(row.employeeId)}">${esc(row.employeeName)} (${esc(row.employeeId)})</option>`).join('');
    if (currentRows.some(row => row.employeeId === selectedStaff)) staffFilterSelect.value = selectedStaff;

    const rows = visibleRows();
    $('payBillCount').textContent = rows.length;
    $('payBillBody').innerHTML = rows.length ? rows.map(row => `<tr data-row="${esc(row.staffId)}">
      <td>${esc(row.employeeId)}</td><td>${esc(row.employeeName)}</td><td>${esc(row.department || '—')}</td><td>${esc(row.designation || '—')}</td>
      <td>${Number(row.paidDays).toLocaleString(document.documentElement.lang, { maximumFractionDigits: 2 })} / ${row.monthDays}</td>
      <td>${money(row.monthlySalary)}</td><td class="basic-pay">${money(row.basicPay)}</td>
      <td>${input(row, 'allowances')}</td><td>${Number(row.overtimeHours || 0).toFixed(2)}</td><td>${input(row, 'overtimeAmount')}</td>
      <td>${input(row, 'deductions')}</td><td class="net-pay"><strong>${money(row.netPay)}</strong></td>
    </tr>`).join('') : `<tr><td colspan="12" class="empty-state">${esc(t('NoStaffForAttendance'))}</td></tr>`;
    renderSummary(rows);
  }

  function renderSummary(rows = visibleRows()) {
    const departments = new Map();
    rows.forEach(row => {
      const key = row.department || '—';
      const summary = departments.get(key) || { employees: 0, basicPay: 0, allowances: 0, overtimeAmount: 0, deductions: 0, netPay: 0 };
      summary.employees++;
      ['basicPay', 'allowances', 'overtimeAmount', 'deductions', 'netPay'].forEach(field => summary[field] += Number(row[field] || 0));
      departments.set(key, summary);
    });
    const summaries = Array.from(departments.entries());
    // a card per department, led by an overall total card
    const card = (name, row, total) => `<div class="pay-dept-card${total ? ' total' : ''}">
      <div class="pay-dept-head">
        <div class="pay-dept-name" title="${esc(name)}">${esc(name)}</div>
        <span class="pay-dept-emp" title="${esc(t('Employees'))}"><i class="fa-solid fa-user"></i>${row.employees}</span>
      </div>
      <div><div class="pay-dept-net-label">${esc(t('NetPay'))}</div><div class="pay-dept-net">${money(row.netPay)}</div></div>
    </div>`;
    const overall = summaries.reduce((sum, [, row]) => {
      ['employees', 'basicPay', 'allowances', 'overtimeAmount', 'deductions', 'netPay'].forEach(field => sum[field] += row[field]);
      return sum;
    }, { employees: 0, basicPay: 0, allowances: 0, overtimeAmount: 0, deductions: 0, netPay: 0 });
    $('payBillSummaryBody').innerHTML = summaries.length
      ? card(t('GrandTotal'), overall, true) + summaries.map(([department, row]) => card(department, row, false)).join('')
      : `<div class="empty-state" style="grid-column:1/-1">${esc(t('NoStaffForAttendance'))}</div>`;
    $('payBillFoot').innerHTML = `<tr><td colspan="6"><strong>${esc(t('GrandTotal'))}</strong></td><td>${money(rows.reduce((sum, row) => sum + Number(row.basicPay || 0), 0))}</td>
      <td>${money(rows.reduce((sum, row) => sum + Number(row.allowances || 0), 0))}</td><td></td>
      <td>${money(rows.reduce((sum, row) => sum + Number(row.overtimeAmount || 0), 0))}</td>
      <td>${money(rows.reduce((sum, row) => sum + Number(row.deductions || 0), 0))}</td>
      <td><strong>${money(rows.reduce((sum, row) => sum + Number(row.netPay || 0), 0))}</strong></td></tr>`;
  }

  function updateAdjustment(event) {
    const field = event.target.dataset.key;
    const row = currentRows.find(item => item.staffId === event.target.dataset.staff);
    if (!row || !field) return;
    row[field] = Math.max(0, Number(event.target.value || 0));
    row.netPay = Math.max(0, row.basicPay + row.allowances + row.overtimeAmount - row.deductions);
    hasUnsavedChanges = true;
    const line = event.target.closest('tr');
    line.querySelector('.net-pay').innerHTML = `<strong>${money(row.netPay)}</strong>`;
    renderSummary();
  }

  async function generate() {
    if (!confirm(t('ConfirmGeneratePayBill'))) return;
    setError('');
    try {
      const response = await fetch('/Payroll/Generate', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json', RequestVerificationToken: token() },
        body: JSON.stringify(Object.assign(period(), {
          rows: currentRows.map(row => ({
            staffId: row.staffId,
            allowances: Number(row.allowances || 0),
            overtimeAmount: Number(row.overtimeAmount || 0),
            deductions: Number(row.deductions || 0)
          }))
        }))
      });
      const data = await response.json();
      if (!response.ok) {
        setError(Object.values(data.errors || {})[0] || t('PayrollSaveFailed'));
        return;
      }
      await load();
      if (typeof showToast === 'function') showToast(t('Success'), data.message || t('PayrollSaved'), 'success');
    } catch (error) {
      console.error(error);
      setError(t('CouldNotReachServer'));
    }
  }

  document.addEventListener('DOMContentLoaded', () => {
    ['payBillMonth', 'payBillYear'].forEach(id => $(id).addEventListener('change', load));
    $('payBillStaffFilter').addEventListener('change', render);
    $('payBillBody').addEventListener('input', updateAdjustment);
    $('payBillGenerate').addEventListener('click', generate);
    load();
  });
})();
