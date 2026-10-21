/* ============================================================
   IMSB Admin Portal – charts.js
   Module-level chart helpers (sales, inventory, HR pages)
   ============================================================ */

/* ── Color Palette ───────────────────────────────────────────── */
const CHART_COLORS = [
  '#2558a8','#e85d04','#1b7a3e','#6366f1',
  '#0369a1','#b45309','#be123c','#0891b2',
  '#7c3aed','#059669'
];

/* ── Sales Breakdown (sales.html) ───────────────────────────── */
function initSalesBreakdownChart(canvasId, labels, data) {
  const ctx = document.getElementById(canvasId);
  if (!ctx || typeof Chart === 'undefined') return;
  return new Chart(ctx, {
    type: 'bar',
    data: {
      labels,
      datasets: [{
        label: 'Sales (₹)',
        data,
        backgroundColor: CHART_COLORS.slice(0, data.length),
        borderRadius: 5,
        borderSkipped: false
      }]
    },
    options: {
      responsive: true,
      maintainAspectRatio: false,
      plugins: {
        legend: { display: false },
        tooltip: { callbacks: { label: c => '₹' + c.parsed.y.toLocaleString('en-IN') } }
      },
      scales: {
        x: { grid: { display: false } },
        y: { ticks: { callback: v => '₹' + (v >= 1000 ? (v/1000).toFixed(0)+'K' : v) } }
      }
    }
  });
}

/* ── Stock Level Chart (inventory.html) ─────────────────────── */
// data (optional): { labels, current, minimum, currentLabel, minimumLabel } — sample figures are used without it
function initStockLevelChart(canvasId, data) {
  const ctx = document.getElementById(canvasId);
  if (!ctx || typeof Chart === 'undefined') return;

  const labels = data ? data.labels : ['Textiles','Handicrafts','Jewellery','Pottery','Paintings','Herbal','Food'];
  const current = data ? data.current : [280, 145, 200, 60, 95, 80, 170];
  const minimum = data ? data.minimum : [100, 80, 100, 50, 60, 50, 80];

  // redrawn after every inventory change; Chart.js refuses to reuse a canvas that still has a chart
  const existing = Chart.getChart(ctx);
  if (existing) existing.destroy();

  return new Chart(ctx, {
    type: 'bar',
    data: {
      labels,
      datasets: [
        { label: (data && data.currentLabel) || 'Current Stock', data: current, backgroundColor: 'rgba(37,88,168,.7)', borderRadius: 4 },
        { label: (data && data.minimumLabel) || 'Min Required',  data: minimum, backgroundColor: 'rgba(232,93,4,.5)',  borderRadius: 4 }
      ]
    },
    options: {
      responsive: true,
      maintainAspectRatio: false,
      plugins: { legend: { position: 'top' } },
      scales: { x: { grid: { display: false } }, y: { beginAtZero: true } }
    }
  });
}

/* ── Staff Dept Chart (staff.html) ──────────────────────────── */
function StaffDeptChart(canvasId, labels, data) {
  const ctx = document.getElementById(canvasId);
  if (!ctx || typeof Chart === 'undefined') return;
  const useLabels = Array.isArray(labels) && labels.length ? labels : ['Sales','Admin','Security','Housekeeping','Accounts','IT Support'];
  const useData = Array.isArray(data) && data.length ? data : [18, 8, 12, 10, 6, 4];
  // re-rendered on every filter change; Chart.js refuses to reuse a canvas that still has a chart
  const existing = Chart.getChart(ctx);
  if (existing) existing.destroy();
  return new Chart(ctx, {
    type: 'pie',
    data: {
      labels: useLabels,
      datasets: [{ data: useData, backgroundColor: CHART_COLORS.slice(0, useData.length), borderWidth: 2 }]
    },
    options: {
      responsive: true,
      maintainAspectRatio: false,
      plugins: { legend: { position: 'right', labels: { font: { size: 11 } } } }
    }
  });
}

/* ── Monthly Sales Trend (reports.html) ─────────────────────── */
function initMonthlyTrend(canvasId) {
  const ctx = document.getElementById(canvasId);
  if (!ctx || typeof Chart === 'undefined') return;
  return new Chart(ctx, {
    type: 'line',
    data: {
      labels: ['Jan','Feb','Mar','Apr','May','Jun','Jul','Aug','Sep','Oct','Nov','Dec'],
      datasets: [
        { label: 'FY 2025-26', data: [980000,1050000,1120000,1200000,1180000,null,null,null,null,null,null,null],
          borderColor: '#2558a8', backgroundColor: 'rgba(37,88,168,.1)', fill: true, tension: .4, borderWidth: 2.5 },
        { label: 'FY 2024-25', data: [820000,870000,910000,980000,1020000,1060000,1010000,990000,1040000,1080000,1120000,1150000],
          borderColor: '#e85d04', backgroundColor: 'rgba(232,93,4,.07)', fill: true, tension: .4, borderWidth: 2, borderDash: [5,3] }
      ]
    },
    options: {
      responsive: true,
      maintainAspectRatio: false,
      plugins: { legend: { position: 'top' }, tooltip: { callbacks: { label: c => '₹' + c.parsed.y.toLocaleString('en-IN') } } },
      scales: {
        x: { grid: { display: false } },
        y: { ticks: { callback: v => '₹' + (v/100000).toFixed(1)+'L' } }
      }
    }
  });
}

/* ── Artisan Status Donut (artisans.html) ───────────────────── */
function initArtisanStatusChart(canvasId) {
  const ctx = document.getElementById(canvasId);
  if (!ctx || typeof Chart === 'undefined') return;
  return new Chart(ctx, {
    type: 'doughnut',
    data: {
      labels: typeof IMSBT === 'function' ? [IMSBT('Active'), IMSBT('OnLeave'), IMSBT('RenewalPending')] : ['Active','On Leave','Pending Renewal'],
      datasets: [{ data: [72, 10, 7], backgroundColor: ['#2558a8','#f59e0b','#e85d04'], borderWidth: 2 }]
    },
    options: {
      responsive: true,
      maintainAspectRatio: false,
      plugins: { legend: { position: 'bottom' } },
      cutout: '65%'
    }
  });
}

/* expose globally */
window.IMSBCharts = {
  initSalesBreakdownChart,
  initStockLevelChart,    // Inventory page
  StaffDeptChart,         // Staff page
  initMonthlyTrend,       // Reports page
  initArtisanStatusChart  // Artisans page
};
