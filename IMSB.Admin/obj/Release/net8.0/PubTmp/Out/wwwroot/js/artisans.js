// Artisan Management: directory + filters, status chart, new application / edit / delete, profile and export
// through the /Artisans endpoints. Localized strings come from window.ArtisansI18n (Views/Artisans/Index.cshtml).
(function ArtisansModule(){
  let artisans = [];
  let categories = [];
  let loadSeq = 0;
  let saving = false;
  let formDirty = false;
  let chart = null;
  let profileCode = null;

  // ── i18n / formatting ─────────────────────────────────────────
  const I18N = window.ArtisansI18n || {};
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
  const money = v => '₹' + Number(v || 0).toLocaleString('en-IN', { maximumFractionDigits: 2 });
  function formatDate(iso){
    const m = /^(\d{4})-(\d{2})-(\d{2})$/.exec(iso || '');
    if (!m) return iso || '—';
    const d = new Date(+m[1], +m[2] - 1, +m[3]);
    return `${m[3]} ${d.toLocaleString(lang(), { month: 'short' })} ${m[1]}`;
  }
  function monthYear(iso){
    const m = /^(\d{4})-(\d{2})/.exec(iso || '');
    return m ? new Date(+m[1], +m[2] - 1, 1).toLocaleDateString(lang(), { month: 'short', year: 'numeric' }) : '—';
  }
  function todayIso(){ const d = new Date(); return `${d.getFullYear()}-${String(d.getMonth()+1).padStart(2,'0')}-${String(d.getDate()).padStart(2,'0')}`; }
  function yearsAgoIso(n){ const d = new Date(); d.setFullYear(d.getFullYear() - n); return `${d.getFullYear()}-${String(d.getMonth()+1).padStart(2,'0')}-${String(d.getDate()).padStart(2,'0')}`; }
  const token = () => document.querySelector('#artisanForm input[name="__RequestVerificationToken"]')?.value || '';
  const toast = (title, msg, type) => { if (typeof showToast === 'function') showToast(title, msg, type); };
  const qs = obj => Object.entries(obj).filter(([, v]) => v !== '' && v != null).map(([k, v]) => `${k}=${encodeURIComponent(v)}`).join('&');
  const initials = name => (name || '').split(/\s+/).filter(Boolean).map(x => x[0]).slice(0, 2).join('').toUpperCase();

  const STATUS = {
    ACTIVE: ['Active', 'badge-success', '#1b7a3e'],
    ON_LEAVE: ['OnLeave', 'badge-warning', '#f59e0b'],
    RENEWAL_DUE: ['RenewalPending', 'badge-info', '#e85d04'],
    INACTIVE: ['Inactive', 'badge-neutral', '#94a3b8']
  };
  const statusLabel = s => t((STATUS[s] || STATUS.ACTIVE)[0]);
  const statusBadge = s => `<span class="badge ${(STATUS[s] || STATUS.ACTIVE)[1]}">${esc(statusLabel(s))}</span>`;

  // ── Load + render ─────────────────────────────────────────────
  function filters(){
    return { q: $('artisanSearch').value.trim(), status: $('filterStatus').value, categoryId: $('filterCategory').value,
      district: $('filterDistrict').value, stall: $('filterStall').value };
  }

  async function load(){
    const seq = ++loadSeq;
    try {
      const res = await fetch('/Artisans/Data?' + qs(filters()));
      if (!res.ok){ console.warn('Artisans: data request failed', res.status); return; }
      const json = await res.json();
      if (seq !== loadSeq) return;
      artisans = json.artisans || [];
      categories = json.categories || [];
      renderStats(json.stats);
      renderSelects(json.districts || []);
      renderChart(json.chart);
      renderTable();
    } catch (e) {
      console.error(e);
      toast(t('Error'), t('CouldNotReachServer'), 'error');
    }
  }

  function renderStats(s){
    const set = (id, v) => { const el = $(id); if (el) el.textContent = v; };
    set('statTotal', s.total); set('statActive', s.active); set('statOnLeave', s.onLeave); set('statRenewal', s.renewalPending);
    const note = $('withStallNote');
    if (note) note.textContent = (note.dataset.format || '{0}').split('{0}').join(s.withStall);
  }

  let selectsReady = false;
  function renderSelects(districts){
    if (selectsReady) return;
    const opts = list => list.map(o => `<option value="${esc(o.value)}">${esc(o.label)}</option>`).join('');
    const cat = categories.map(c => ({ value: c.id, label: c.name }));
    const dist = districts.map(d => ({ value: d, label: d }));
    $('filterCategory').insertAdjacentHTML('beforeend', opts(cat));
    $('arCategory').insertAdjacentHTML('beforeend', opts(cat));
    $('filterDistrict').insertAdjacentHTML('beforeend', opts(dist));
    $('arDistrict').insertAdjacentHTML('beforeend', opts(dist));
    selectsReady = true;
  }

  function renderChart(c){
    const canvas = $('artisanStatusChart');
    const total = c.values.reduce((a, b) => a + b, 0);
    $('chartEmpty').style.display = total ? 'none' : 'flex';
    canvas.style.display = total ? '' : 'none';
    if (typeof Chart === 'undefined' || !total) return;
    if (chart) chart.destroy();
    chart = new Chart(canvas, {
      type: 'doughnut',
      data: {
        labels: c.statuses.map(statusLabel),
        datasets: [{ data: c.values, backgroundColor: c.statuses.map(s => (STATUS[s] || STATUS.ACTIVE)[2]), borderWidth: 2 }]
      },
      options: {
        responsive: true, maintainAspectRatio: false, cutout: '65%',
        plugins: { legend: { position: 'bottom', labels: { boxWidth: 10, font: { size: 11 } } } }
      }
    });
  }

  function renderTable(){
    const body = $('artisanBody');
    $('directoryCount').textContent = artisans.length;
    if (!artisans.length){ body.innerHTML = `<tr><td colspan="9" class="empty-state">${esc(t('NoArtisansFound'))}</td></tr>`; return; }
    body.innerHTML = artisans.map(a => `<tr>
      <td><code style="font-size:.7rem;color:var(--primary)">${esc(a.code)}</code></td>
      <td>
        <div style="display:flex;align-items:center;gap:9px">
          <div style="width:32px;height:32px;border-radius:50%;background:var(--primary-xlight);display:flex;align-items:center;justify-content:center;font-size:.7rem;font-weight:700;color:var(--primary)">${esc(initials(a.fullName))}</div>
          <strong>${esc(a.fullName)}</strong>
        </div>
      </td>
      <td>${esc(a.categoryName || '—')}</td>
      <td>${a.stallCode ? `<span class="badge badge-primary">${esc(a.stallCode)}</span>` : '—'}</td>
      <td>${esc(a.mobile)}</td>
      <td>${esc(a.district)}</td>
      <td>${esc(monthYear(a.joinedDate))}</td>
      <td>${statusBadge(a.status)}</td>
      <td style="white-space:nowrap">
        <button type="button" class="btn btn-ghost btn-sm btn-icon" data-act="view" data-code="${esc(a.code)}" title="${esc(t('View'))}" aria-label="${esc(t('View'))}"><i class="fa-solid fa-eye"></i></button>
        <button type="button" class="btn btn-ghost btn-sm btn-icon" data-act="edit" data-code="${esc(a.code)}" title="${esc(t('Edit'))}" aria-label="${esc(t('Edit'))}"><i class="fa-solid fa-pen"></i></button>
        <button type="button" class="btn btn-ghost btn-sm btn-icon" data-act="delete" data-code="${esc(a.code)}" title="${esc(t('Delete'))}" aria-label="${esc(t('Delete'))}" style="color:var(--danger)"><i class="fa-solid fa-trash"></i></button>
      </td>
    </tr>`).join('');
  }

  // ── Profile ───────────────────────────────────────────────────
  async function fetchArtisan(code){
    const res = await fetch('/Artisans/Get/' + encodeURIComponent(code));
    if (res.status === 404){ toast(t('Error'), t('ArtisanNotFound'), 'error'); load(); return null; }
    if (!res.ok){ toast(t('Error'), t('CouldNotReachServer'), 'error'); return null; }
    return res.json();
  }

  async function view(code){
    const a = await fetchArtisan(code);
    if (!a) return;
    profileCode = a.code;
    const set = (id, v) => { $(id).textContent = v; };
    set('profileAvatar', initials(a.fullName));
    set('profileName', a.fullName);
    set('profileRole', t('ArtisanOfCategory', a.categoryName || ''));
    set('profileStall', a.stallCode ? t('StallLabel', a.stallCode) : t('NoStallAssigned'));
    set('profileSince', t('SinceDate', monthYear(a.joinedDate)));
    set('profileId', a.code);
    $('profileStatus').innerHTML = statusBadge(a.status);
    set('profileMobile', a.mobile);
    set('profileDistrict', a.district);
    set('profileAadhaar', a.aadhaarMasked);
    set('profileYears', t('YearsCount', a.yearsOfExperience));
    set('profileSales', money(a.monthSales));
    set('profileProducts', a.productsListed);
    $('profileSkillsWrap').style.display = a.skills ? '' : 'none';
    set('profileSkills', a.skills || '');
    openModal('artisanProfileModal');
  }

  // ── New application / edit ────────────────────────────────────
  function clearErrors(){
    const form = $('artisanForm');
    form.querySelectorAll('.form-group.has-error').forEach(g => g.classList.remove('has-error'));
    form.querySelectorAll('.form-error').forEach(e => { e.textContent = ''; });
  }
  function showErrors(errors){
    let first = null, n = 0;
    Object.entries(errors).forEach(([k, msg]) => {
      const el = $('err_' + k);
      if (!el) return;
      n++;
      el.textContent = msg;
      const group = el.closest('.form-group');
      group?.classList.add('has-error');
      if (!first) first = group?.querySelector('input,select,textarea');
    });
    first?.focus();
    return n;
  }

  function openForm(a){
    $('artisanForm').reset();
    clearErrors();
    $('arEditing').value = a ? a.code : '';
    $('arName').value = a?.fullName || '';
    $('arDob').value = a?.dateOfBirth || '';
    $('arGender').value = a?.gender || 'Female';
    $('arMobile').value = a?.mobile || '';
    $('arEmail').value = a?.email || '';
    $('arAadhaar').value = '';
    $('arAadhaarReq').style.display = a ? 'none' : '';
    $('arAadhaarHint').textContent = a ? t('AadhaarKeepHint', a.aadhaarMasked) : t('AadhaarStoredHint');
    $('arAddress').value = a?.address || '';
    $('arDistrict').value = a?.district || '';
    $('arJoined').value = a?.joinedDate || todayIso();
    $('arCategory').value = a?.categoryId || '';
    $('arYears').value = a ? a.yearsOfExperience : 0;
    $('arSkills').value = a?.skills || '';
    $('arStatus').value = a?.status || 'ACTIVE';
    $('arRemarks').value = a?.remarks || '';
    $('artisanModalText').textContent = a ? t('EditArtisan') : t('NewArtisanApplication');
    $('artisanSaveText').textContent = a ? t('SaveChanges') : t('SubmitApplication');
    $('arDob').max = yearsAgoIso(18);
    $('arJoined').max = todayIso();
    formDirty = false;
    openModal('artisanModal');
    $('arName').focus();
  }
  function closeForm(force){
    if (!force && formDirty && !confirm(t('DiscardChanges'))) return;
    formDirty = false;
    closeModal('artisanModal');
  }

  function formValues(){
    return {
      fullName: $('arName').value.trim(),
      dateOfBirth: $('arDob').value,
      gender: $('arGender').value,
      mobile: $('arMobile').value.trim(),
      email: $('arEmail').value.trim() || null,
      address: $('arAddress').value.trim(),
      aadhaar: $('arAadhaar').value.trim() || null,
      district: $('arDistrict').value,
      categoryId: $('arCategory').value,
      yearsOfExperience: Number($('arYears').value || 0),
      skills: $('arSkills').value.trim() || null,
      status: $('arStatus').value,
      joinedDate: $('arJoined').value || null,
      remarks: $('arRemarks').value.trim() || null
    };
  }

  // mirrors the server rules; the server has the final say (duplicates, Aadhaar checksum)
  function validate(v, editing){
    const e = {};
    if (v.fullName.length < 2 || v.fullName.length > 100) e.full_name = t('ValFullName');
    if (!v.dateOfBirth) e.date_of_birth = t('ValDobRequired');
    else if (v.dateOfBirth > todayIso()) e.date_of_birth = t('ValDobFuture');
    else if (v.dateOfBirth > yearsAgoIso(18)) e.date_of_birth = t('ValArtisanMinAge');
    const mobile = v.mobile.replace(/[\s-]/g, '').replace(/^\+91/, '').replace(/^0(?=\d{10}$)/, '');
    if (!/^[6-9]\d{9}$/.test(mobile)) e.mobile = t('ValArtisanMobile');
    if (v.email && !/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(v.email)) e.email = t('ValEmail');
    if (v.address.length < 5) e.address = t('ValAddress');
    if ((!editing || v.aadhaar) && !/^[2-9]\d{3}[\s-]?\d{4}[\s-]?\d{4}$/.test(v.aadhaar || '')) e.aadhaar = t('ValAadhaar');
    if (!v.district) e.district = t('ValDistrict');
    if (!v.categoryId) e.category_id = t('ValCraftCategory');
    if (!Number.isInteger(v.yearsOfExperience) || v.yearsOfExperience < 0 || v.yearsOfExperience > 80) e.years_of_experience = t('ValExperience');
    if (v.joinedDate && v.joinedDate > todayIso()) e.joined_date = t('ValJoinFuture');
    return e;
  }

  async function save(){
    if (saving) return;
    clearErrors();
    const editing = $('arEditing').value;
    const v = formValues();
    const errors = validate(v, !!editing);
    if (Object.keys(errors).length){ showErrors(errors); return; }

    saving = true;
    $('artisanSaveBtn').disabled = true;
    try {
      const res = await fetch(editing ? '/Artisans/Update/' + encodeURIComponent(editing) : '/Artisans/Create', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json', 'RequestVerificationToken': token() },
        body: JSON.stringify(v)
      });
      const json = await res.json().catch(() => ({}));
      if (res.status === 404){ toast(t('Error'), t('ArtisanNotFound'), 'error'); closeForm(true); load(); return; }
      if (!res.ok){
        const shown = showErrors(json.errors || {});
        toast(t('Error'), shown ? t('CheckForm') : `${t('CheckForm')} (${res.status})`, 'error');
        return;
      }
      toast(t('Success'), t(editing ? 'ArtisanUpdated' : 'ArtisanRegistered', json.code), 'success');
      closeForm(true);
      await load();
    } catch (e) {
      console.error(e);
      toast(t('Error'), t('CouldNotReachServer'), 'error');
    } finally {
      saving = false;
      $('artisanSaveBtn').disabled = false;
    }
  }

  async function edit(code){
    const a = await fetchArtisan(code);
    if (a) openForm(a);
  }

  async function remove(code){
    const a = artisans.find(x => x.code === code);
    if (!confirm(t('ConfirmDeleteArtisan', a ? a.fullName : code, code))) return;
    try {
      const res = await fetch('/Artisans/Delete/' + encodeURIComponent(code), { method: 'POST', headers: { 'RequestVerificationToken': token() } });
      const json = await res.json().catch(() => ({}));
      if (res.status === 404){ toast(t('Error'), t('ArtisanNotFound'), 'error'); load(); return; }
      if (!res.ok){ toast(t('Error'), Object.values(json.errors || {})[0] || `${t('CheckForm')} (${res.status})`, 'error'); return; }
      toast(t('Success'), t('ArtisanDeleted', code), 'success');
      load();
    } catch (e) {
      console.error(e);
      toast(t('Error'), t('CouldNotReachServer'), 'error');
    }
  }

  // ── Wiring ────────────────────────────────────────────────────
  function debounce(fn, ms){ let h; return (...a) => { clearTimeout(h); h = setTimeout(() => fn(...a), ms); }; }

  document.addEventListener('DOMContentLoaded', () => {
    $('artisanSearch').addEventListener('input', debounce(load, 250));
    ['filterStatus', 'filterCategory', 'filterDistrict', 'filterStall'].forEach(id => $(id).addEventListener('change', load));
    $('filterResetBtn').addEventListener('click', () => {
      ['artisanSearch', 'filterStatus', 'filterCategory', 'filterDistrict', 'filterStall'].forEach(id => { $(id).value = ''; });
      load();
    });

    $('artisanBody').addEventListener('click', e => {
      const btn = e.target.closest('[data-act]');
      if (!btn) return;
      if (btn.dataset.act === 'view') view(btn.dataset.code);
      else if (btn.dataset.act === 'edit') edit(btn.dataset.code);
      else if (btn.dataset.act === 'delete') remove(btn.dataset.code);
    });
    $('profileEditBtn').addEventListener('click', () => { closeModal('artisanProfileModal'); if (profileCode) edit(profileCode); });

    $('newArtisanBtn').addEventListener('click', () => openForm(null));
    $('artisanSaveBtn').addEventListener('click', save);
    $('artisanForm').addEventListener('submit', e => { e.preventDefault(); save(); });
    $('artisanForm').addEventListener('input', () => { formDirty = true; });
    $('artisanCancelBtn').addEventListener('click', () => closeForm(false));
    $('artisanModalClose').addEventListener('click', () => closeForm(false));

    $('artisansExportBtn').addEventListener('click', () => { window.location.href = '/Artisans/Export?' + qs(filters()); });

    load();
  });
})();
