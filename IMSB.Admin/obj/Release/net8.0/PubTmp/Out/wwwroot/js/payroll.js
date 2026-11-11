(function () {
  const $ = id => document.getElementById(id);
  const I = window.PayBillI18n || {};
  const t = key => I[key] || key;
  const esc = value => String(value == null ? '' : value).replace(/[&<>"']/g, c =>
    ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]));
  const qs = values => Object.entries(values).map(([key, value]) => `${key}=${encodeURIComponent(value)}`).join('&');
  const money = value => '₹' + Number(value || 0).toLocaleString('en-IN', { minimumFractionDigits: 2, maximumFractionDigits: 2 });
  const num = value => Number(value || 0).toLocaleString(document.documentElement.lang, { maximumFractionDigits: 2 });
  let currentRows = [];
  let allDepartments = [];

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
      allDepartments = data.departments || [];
      $('payBillPeriod').textContent = `${new Date(+data.year, +data.month - 1, 1).toLocaleDateString(document.documentElement.lang, { month: 'long', year: 'numeric' })} · ${data.monthDays} days`;
      render();
    } catch (error) {
      console.error(error);
      setError(t('CouldNotReachServer'));
    }
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
      <td>${num(row.paidDays)} / ${row.monthDays}</td>
      <td>${money(row.monthlySalary)}</td><td>${num(row.overtimeHours)}</td><td>${money(row.overtimeAmount)}</td>
      <td class="${Number(row.lopDays) > 0 ? 'lop' : ''}">${num(row.lopDays)}</td>
      <td class="${Number(row.lopDeduction) > 0 ? 'lop' : ''}">${money(row.lopDeduction)}</td>
      <td class="net-pay"><strong>${money(row.netPay)}</strong></td>
    </tr>`).join('') : `<tr><td colspan="11" class="empty-state">${esc(t('NoStaffForAttendance'))}</td></tr>`;
    renderSummary(rows);
  }

  function renderSummary(rows = visibleRows()) {
    // every staff category gets a card, showing zero when it has nobody on this bill
    const departments = new Map(allDepartments.map(name => [name, { employees: 0, netPay: 0 }]));
    rows.forEach(row => {
      const key = row.department || '—';
      const summary = departments.get(key) || { employees: 0, netPay: 0 };
      summary.employees++;
      summary.netPay += Number(row.netPay || 0);
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
      sum.employees += row.employees;
      sum.netPay += row.netPay;
      return sum;
    }, { employees: 0, netPay: 0 });
    $('payBillSummaryBody').innerHTML = card(t('GrandTotal'), overall, true)
      + summaries.map(([department, row]) => card(department, row, false)).join('');
    const sum = key => rows.reduce((total, row) => total + Number(row[key] || 0), 0);
    // label over the first five columns; then salary, OT hours, OT amount, LOP days, LOP deduction, net pay
    const totals = [money(sum('monthlySalary')), num(sum('overtimeHours')), money(sum('overtimeAmount')),
      num(sum('lopDays')), money(sum('lopDeduction')), money(sum('netPay'))];
    $('payBillFoot').innerHTML = `<tr><td colspan="5"><strong>${esc(t('GrandTotal'))}</strong></td>
      ${totals.map(value => `<td><strong>${value}</strong></td>`).join('')}</tr>`;
  }

  // the same month and staff member as on screen
  const exportQuery = () => qs(Object.assign(period(), { employeeId: staffFilter() }));

  function exportExcel() {
    window.location.href = '/Payroll/Export?' + exportQuery();
  }

  // html2pdf.js (MIT) turns the pay bill document into a PDF in the browser, so Hindi / Tamil / Telugu text is drawn
  // exactly as the browser shows it. Loaded on first use only.
  const HTML2PDF_URL = 'https://cdnjs.cloudflare.com/ajax/libs/html2pdf.js/0.10.1/html2pdf.bundle.min.js';
  let html2pdfLoading = null;
  function loadHtml2Pdf() {
    if (window.html2pdf) return Promise.resolve(window.html2pdf);
    html2pdfLoading = html2pdfLoading || new Promise((resolve, reject) => {
      const script = document.createElement('script');
      script.src = HTML2PDF_URL;
      script.onload = () => (window.html2pdf ? resolve(window.html2pdf) : reject(new Error('html2pdf missing')));
      script.onerror = () => { html2pdfLoading = null; reject(new Error('html2pdf could not be loaded')); };
      document.head.appendChild(script);
    });
    return html2pdfLoading;
  }

  // pay_bill_202610.pdf, or pay_bill_202610_EMP001.pdf for one employee (same name as the Excel file)
  function pdfFileName() {
    const { year, month } = period();
    const employeeId = staffFilter().replace(/[^\w-]/g, '');
    return `pay_bill_${year}${String(month).padStart(2, '0')}${employeeId ? '_' + employeeId : ''}.pdf`;
  }

  let pdfBusy = false;
  // downloads the month's pay bill (as on screen) as a PDF file: A4 landscape, header row repeated, rows kept whole
  async function exportPdf() {
    if (pdfBusy) return;
    pdfBusy = true;
    const button = $('payBillExportBtn');
    button.disabled = true;
    button.setAttribute('aria-busy', 'true');
    setError('');
    try {
      const [response, html2pdf] = await Promise.all([fetch('/Payroll/PdfDocument?' + exportQuery()), loadHtml2Pdf()]);
      if (!response.ok) { setError(t('PdfExportFailed')); return; }
      const html = await response.text();
      await html2pdf().set({
        margin: [8, 8, 8, 8],
        filename: pdfFileName(),
        image: { type: 'jpeg', quality: 0.95 },
        html2canvas: { scale: 2, backgroundColor: '#ffffff' },
        jsPDF: { unit: 'mm', format: 'a4', orientation: 'landscape' },
        pagebreak: { mode: ['css', 'legacy'], avoid: 'tr' }
      }).from(html).save();
    } catch (error) {
      console.error(error);
      setError(t('PdfExportFailed'));
    } finally {
      pdfBusy = false;
      button.disabled = false;
      button.removeAttribute('aria-busy');
    }
  }

  // Export dropdown: PDF / Excel. Opens on click (or Enter / Space / ↓), arrow keys move between the items,
  // Escape or a click elsewhere closes it.
  function setUpExportMenu() {
    const button = $('payBillExportBtn');
    const list = $('payBillExportList');
    const items = () => Array.from(list.querySelectorAll('[role="menuitem"]'));
    const isOpen = () => !list.hidden;

    function open(focusIndex = 0) {
      list.hidden = false;
      button.setAttribute('aria-expanded', 'true');
      items()[focusIndex]?.focus();
    }
    function close(returnFocus) {
      if (!isOpen()) return;
      list.hidden = true;
      button.setAttribute('aria-expanded', 'false');
      if (returnFocus) button.focus();
    }

    button.addEventListener('click', () => (isOpen() ? close(false) : open()));
    button.addEventListener('keydown', e => {
      if (e.key === 'ArrowDown') { e.preventDefault(); open(0); }
      else if (e.key === 'ArrowUp') { e.preventDefault(); open(items().length - 1); }
    });
    list.addEventListener('keydown', e => {
      const all = items();
      const index = all.indexOf(document.activeElement);
      if (e.key === 'ArrowDown') { e.preventDefault(); all[(index + 1) % all.length].focus(); }
      else if (e.key === 'ArrowUp') { e.preventDefault(); all[(index - 1 + all.length) % all.length].focus(); }
      else if (e.key === 'Home') { e.preventDefault(); all[0].focus(); }
      else if (e.key === 'End') { e.preventDefault(); all[all.length - 1].focus(); }
      else if (e.key === 'Escape') { e.preventDefault(); close(true); }
      else if (e.key === 'Tab') close(false);
    });
    document.addEventListener('click', e => { if (!$('payBillExportMenu').contains(e.target)) close(false); });

    $('payBillExportPdfBtn').addEventListener('click', () => { close(true); exportPdf(); });
    $('payBillExportExcelBtn').addEventListener('click', () => { close(true); exportExcel(); });
  }

  document.addEventListener('DOMContentLoaded', () => {
    ['payBillMonth', 'payBillYear'].forEach(id => $(id).addEventListener('change', load));
    $('payBillStaffFilter').addEventListener('change', render);
    setUpExportMenu();
    load();
  });
})();
