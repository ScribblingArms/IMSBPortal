(function MonthlyAttendanceCalendar(){
  const I18N = window.AttendanceI18n || {};
  const $ = id => document.getElementById(id);
  const t = key => I18N[key] || key;
  const esc = value => String(value == null ? '' : value)
    .replace(/[&<>"']/g, character => ({ '&':'&amp;', '<':'&lt;', '>':'&gt;', '"':'&quot;', "'":'&#39;' }[character]));
  const qs = values => Object.entries(values)
    .filter(([, value]) => value !== '' && value != null)
    .map(([key, value]) => `${key}=${encodeURIComponent(value)}`).join('&');
  let requestSequence = 0, searchTimer = null;

  const monthKey = () => `${$('monthlyYear').value}-${String($('monthlyMonth').value).padStart(2, '0')}`;
  const filters = () => ({
    month: monthKey(),
    type: $('monthlyType').value,
    q: $('monthlySearch').value.trim(),
    status: $('monthlyStatus').value
  });

  async function loadMonthly(){
    const query = filters();
    const sequence = ++requestSequence;
    const error = $('monthlyError');
    error.textContent = '';
    error.style.display = '';

    try {
      const response = await fetch('/Attendance/Monthly?' + qs(query));
      if (sequence !== requestSequence) return;
      if (!response.ok){
        const json = response.status === 400 ? await response.json().catch(() => ({})) : {};
        error.textContent = Object.values(json.errors || {})[0] || `${t('CouldNotReachServer')} (${response.status})`;
        error.style.display = 'block';
        $('monthlyCount').textContent = '0';
        $('monthlyHead').innerHTML = '';
        $('monthlyBody').innerHTML = '';
        return;
      }

      const calendar = await response.json();
      if (sequence !== requestSequence) return;
      $('monthlyType').innerHTML = `<option value="">${esc(t('AllTypes'))}</option>` +
        calendar.types.map(value => `<option value="${esc(value)}">${esc(value)}</option>`).join('');
      $('monthlyType').value = query.type;
      renderMonthlyCalendar(calendar);
      $('monthlyCount').textContent = String(calendar.rows.length);
    } catch (exception) {
      console.error(exception);
      error.textContent = t('CouldNotReachServer');
      error.style.display = 'block';
      $('monthlyCount').textContent = '0';
      $('monthlyHead').innerHTML = '';
      $('monthlyBody').innerHTML = '';
    }
  }

  function renderMonthlyCalendar(calendar){
    $('monthlyHead').innerHTML = `<tr><th>${esc(t('EmployeeID'))}</th><th>${esc(t('StaffName'))}</th><th>${esc(t('EmployeeType'))}</th>` +
      calendar.days.map(day => `<th>${day}</th>`).join('') + '</tr>';

    if (!calendar.rows.length){
      $('monthlyBody').innerHTML = `<tr><td colspan="${calendar.days.length + 3}" class="empty-state">${esc(t('NoStaffForAttendance'))}</td></tr>`;
      return;
    }

    const marks = {
      PRESENT: ['present', '✓', 'Present'],
      ABSENT: ['absent', '×', 'Absent'],
      LEAVE: ['leave', 'L', 'Leave'],
      HALF_DAY: ['halfday', '½', 'HalfDay']
    };
    $('monthlyBody').innerHTML = calendar.rows.map(row => `<tr>
      <td>${esc(row.employeeId)}</td><td><strong>${esc(row.name)}</strong></td><td>${esc(row.type)}</td>
      ${row.days.map(status => {
        const mark = marks[status];
        return mark
          ? `<td><span class="att-calendar-mark ${mark[0]}" title="${esc(t(mark[2]))}" aria-label="${esc(t(mark[2]))}">${mark[1]}</span></td>`
          : `<td><span class="att-calendar-mark unmarked" title="${esc(t('NotMarked'))}" aria-label="${esc(t('NotMarked'))}">—</span></td>`;
      }).join('')}
    </tr>`).join('');
  }

  document.addEventListener('DOMContentLoaded', () => {
    const today = new Date();
    $('monthlyMonth').innerHTML = Array.from({ length: 12 }, (_, index) => {
      const date = new Date(2000, index, 1);
      return `<option value="${index + 1}">${esc(date.toLocaleDateString(document.documentElement.lang || undefined, { month: 'long' }))}</option>`;
    }).join('');
    $('monthlyMonth').value = String(today.getMonth() + 1);
    const thisYear = today.getFullYear();
    $('monthlyYear').innerHTML = Array.from({ length: 21 }, (_, index) => thisYear + 1 - index)
      .map(year => `<option value="${year}">${year}</option>`).join('');
    $('monthlyYear').value = String(thisYear);

    const defaultMonth = String(today.getMonth() + 1);
    const defaultYear = String(thisYear);
    $('monthlySearch').addEventListener('input', () => {
      clearTimeout(searchTimer);
      searchTimer = setTimeout(loadMonthly, 250);
    });
    ['monthlyMonth', 'monthlyYear', 'monthlyType', 'monthlyStatus'].forEach(id =>
      $(id).addEventListener('change', loadMonthly));
    $('monthlyResetBtn').addEventListener('click', () => {
      $('monthlySearch').value = '';
      $('monthlyStatus').value = '';
      $('monthlyMonth').value = defaultMonth;
      $('monthlyYear').value = defaultYear;
      $('monthlyType').value = '';
      clearTimeout(searchTimer);
      loadMonthly();
    });
    $('monthlyExportBtn').addEventListener('click', () => {
      window.location.href = '/Attendance/MonthlyExport?' + qs(filters());
    });
    loadMonthly();
  });
})();
