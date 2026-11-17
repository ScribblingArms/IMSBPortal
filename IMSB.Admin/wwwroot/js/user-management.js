// User Management page: District Officer and State Admin Officer logins with add, view, update and delete, through the
// /UserManagement endpoints. Localized strings come from window.UserManagementI18n (rendered by Views/UserManagement/Index.cshtml);
// user types and districts come with their localized labels from /UserManagement/Data.
(function UserManagementModule(){
  let rows = [];         // /UserManagement/Data rows
  let userTypes = [];    // [{ value, label }]
  let districts = [];    // [{ value, label }], the 33 Telangana districts
  let editingId = null;  // null while adding
  let saving = false;

  const I18N = window.UserManagementI18n || {};
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
  const token = () => document.querySelector('#umForm input[name="__RequestVerificationToken"]')?.value || '';
  const FIELDS = ['request', 'user_name', 'email', 'password', 'confirm_password', 'mobile', 'user_type', 'district'];
  // ── List ──────────────────────────────────────────────────────
  async function load(){
    try {
      const res = await fetch('/UserManagement/Data');
      if (!res.ok){ toast(t('Error'), t('CouldNotReachServer'), 'error'); return; }
      const json = await res.json();
      rows = json.rows || [];
      userTypes = json.userTypes || [];
      districts = json.districts || [];
      fillDistrictFilter();
      render();
    } catch (e) {
      console.error(e);
      toast(t('Error'), t('CouldNotReachServer'), 'error');
    }
  }

  function fillDistrictFilter(){
    const select = $('umFilterDistrict');
    const current = select.value;
    select.length = 1;   // keep "All districts"
    districts.forEach(d => select.add(new Option(d.label, d.value)));
    select.value = current;
  }

  function displayDate(iso){
    if (!iso) return '—';
    const d = new Date(iso);
    return isNaN(d) ? '—' : d.toLocaleDateString(document.documentElement.lang || undefined, { day: '2-digit', month: 'short', year: 'numeric' });
  }
  function displayDateTime(iso){
    if (!iso) return t('Never');
    const d = new Date(iso);
    return isNaN(d) ? '—' : d.toLocaleString(document.documentElement.lang || undefined, { day: '2-digit', month: 'short', year: 'numeric', hour: 'numeric', minute: '2-digit' });
  }

  function actionButtons(row){
    return [['view', 'fa-eye', t('View'), ''], ['edit', 'fa-pen', t('Edit'), ''], ['delete', 'fa-trash', t('Delete'), 'color:var(--danger)']]
      .map(([act, icon, label, style]) => `<button type="button" class="btn btn-ghost btn-sm btn-icon" data-act="${act}" data-id="${esc(row.id)}"
          title="${esc(label)}" aria-label="${esc(label)}" style="${style}"><i class="fa-solid ${icon}"></i></button>`)
      .join('');
  }

  function render(){
    const q = $('umSearch').value.trim().toLowerCase();
    const district = $('umFilterDistrict').value;
    const shown = rows.filter(r => (!district || r.district === district)
      && (!q || `${r.userName} ${r.email} ${r.mobile} ${r.districtLabel} ${r.district}`.toLowerCase().includes(q)));
    $('umCount').textContent = shown.length;
    $('umBody').innerHTML = shown.length ? shown.map(r => `<tr>
      <td><strong>${esc(r.userName)}</strong></td>
      <td style="overflow-wrap:anywhere">${esc(r.email || '—')}</td>
      <td>${esc(r.mobile || '—')}</td>
      <td>${esc(r.userTypeLabel || r.userType)}</td>
      <td>${esc(r.districtLabel || '—')}</td>
      <td style="white-space:nowrap">${esc(displayDate(r.createdAt))}</td>
      <td style="white-space:nowrap">${actionButtons(r)}</td>
    </tr>`).join('') : `<tr><td colspan="7" class="empty-state" style="text-align:center">${esc(t('NoUsersFound'))}</td></tr>`;
  }

  // ── Add / edit form ───────────────────────────────────────────
  function clearErrors(){
    FIELDS.forEach(k => {
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

  function fillSelect(select, options, placeholderKey, value){
    select.innerHTML = `<option value="">${esc(t(placeholderKey))}</option>` +
      options.map(o => `<option value="${esc(o.value)}">${esc(o.label)}</option>`).join('');
    select.value = value || '';
  }

  function openForm(row){
    editingId = row ? row.id : null;
    touched.clear();
    clearErrors();
    $('umFormTitle').textContent = t(row ? 'EditUser' : 'AddUser');
    $('umSaveLabel').textContent = t(row ? 'UpdateUser' : 'SaveUser');
    $('umUserName').value = row ? row.userName : '';
    $('umEmail').value = row ? (row.email || '') : '';
    $('umMobile').value = row ? (row.mobile || '') : '';
    $('umPassword').value = '';
    $('umConfirmPassword').value = '';
    // on update the password is optional: empty keeps the current one
    $('umPasswordHint').hidden = !row;
    $('umPasswordReq').hidden = !!row;
    $('umConfirmReq').hidden = !!row;
    fillSelect($('umUserType'), userTypes, 'SelectPlaceholder', row ? row.userType : '');
    fillSelect($('umDistrict'), districts, 'SelectDistrict', row ? row.district : '');
    openModal('umFormModal');
    $('umUserName').focus();
  }

  // same as UserFormDto.IsStrongPassword: upper, lower, digit and another character, no spaces
  const isStrong = p => /[A-Z]/.test(p) && /[a-z]/.test(p) && /[0-9]/.test(p) && /[^A-Za-z0-9\s]/.test(p) && !/\s/.test(p);

  function passwordErrors(password, confirm){
    const errors = {};
    if (!password){
      if (!editingId || confirm) errors.password = t('ValPasswordRequired');
    }
    else if (password.length < 8 || password.length > 100) errors.password = t('ValPasswordLength');
    else if (!isStrong(password)) errors.password = t('ValPasswordStrength');
    if (password && !confirm) errors.confirm_password = t('ValConfirmPasswordRequired');
    else if (password && password !== confirm) errors.confirm_password = t('ValPasswordsDoNotMatch');
    return errors;
  }

  // ── Checks while typing: only fields the user has already left (touched) show their error ──
  const touched = new Set();
  function setError(key, msg){
    const el = $('err_' + key);
    el.textContent = msg || '';
    el.closest('.form-group')?.classList.toggle('has-error', !!msg);
  }
  function checkLive(){
    const email = $('umEmail').value.trim();
    if (touched.has('umEmail'))
      setError('email', !email ? t('ValEmailRequired') : (email.length > 200 || !IMSBValidate.email(email)) ? t('ValEmail') : '');
    const errors = passwordErrors($('umPassword').value, $('umConfirmPassword').value);
    if (touched.has('umPassword')) setError('password', errors.password);
    if (touched.has('umConfirmPassword')) setError('confirm_password', errors.confirm_password);
  }

  // same rules as UserFormDto.Validate on the server
  function validate(form){
    const errors = {};
    if (!/^[A-Za-z0-9._@-]{3,100}$/.test(form.userName)) errors.user_name = t('ValUserName');
    if (!form.email) errors.email = t('ValEmailRequired');
    else if (form.email.length > 200 || !IMSBValidate.email(form.email)) errors.email = t('ValEmail');
    Object.assign(errors, passwordErrors(form.password, form.confirmPassword));
    if (!IMSBValidate.mobile(form.mobile)) errors.mobile = t('ValMobile');
    if (!userTypes.some(u => u.value === form.userType)) errors.user_type = t('ValUserType');
    if (!districts.some(d => d.value === form.district)) errors.district = t('ValDistrict');
    return errors;
  }

  async function save(){
    if (saving) return;
    clearErrors();
    const form = {
      userName: $('umUserName').value.trim(),
      email: $('umEmail').value.trim(),
      password: $('umPassword').value,
      confirmPassword: $('umConfirmPassword').value,
      mobile: $('umMobile').value.trim(),
      userType: $('umUserType').value,
      district: $('umDistrict').value
    };
    const errors = validate(form);
    if (Object.keys(errors).length){ showErrors(errors); return; }

    const url = editingId ? '/UserManagement/Update/' + encodeURIComponent(editingId) : '/UserManagement/Create';
    saving = true;
    $('umSaveBtn').disabled = true;
    try {
      const res = await fetch(url, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json', 'RequestVerificationToken': token() },
        body: JSON.stringify(form)
      });
      if (res.status === 400){
        const json = await res.json().catch(() => ({}));
        showErrors(json.errors || { request: t('CheckForm') });
        return;
      }
      if (res.status === 404){ toast(t('Error'), t('CheckForm'), 'error'); closeModal('umFormModal'); await load(); return; }
      if (!res.ok){ toast(t('Error'), `${t('CheckForm')} (${res.status})`, 'error'); return; }
      toast(t('Success'), t(editingId ? 'UserUpdated' : 'UserSaved'), 'success');
      closeModal('umFormModal');
      await load();
    } catch (e) {
      console.error(e);
      toast(t('Error'), t('CouldNotReachServer'), 'error');
    } finally {
      saving = false;
      $('umSaveBtn').disabled = false;
    }
  }

  // ── View / delete ─────────────────────────────────────────────
  function view(row){
    const fields = [
      ['UserNameLabel', row.userName], ['Email', row.email], ['Mobile', row.mobile], ['UserType', row.userTypeLabel || row.userType],
      ['District', row.districtLabel], ['Status', t(row.isActive ? 'Active' : 'Inactive')],
      ['CreatedOn', displayDate(row.createdAt)], ['LastLoginLabel', displayDateTime(row.lastLogin)]
    ];
    $('umViewBody').innerHTML = `<div class="form-grid col-2">
      ${fields.map(([label, value]) => `<div class="form-group"><div class="form-label">${esc(t(label))}</div><div style="font-weight:600;overflow-wrap:anywhere">${esc(value || '—')}</div></div>`).join('')}
    </div>`;
    openModal('umViewModal');
  }

  async function remove(row){
    if (!confirm(t('ConfirmDeleteUser', row.userName))) return;
    try {
      const res = await fetch('/UserManagement/Delete/' + encodeURIComponent(row.id), { method: 'POST', headers: { 'RequestVerificationToken': token() } });
      if (!res.ok && res.status !== 404){ toast(t('Error'), `${t('CheckForm')} (${res.status})`, 'error'); return; }
      if (res.ok) toast(t('Success'), t('UserDeleted'), 'success');
      await load();
    } catch (e) {
      console.error(e);
      toast(t('Error'), t('CouldNotReachServer'), 'error');
    }
  }

  document.addEventListener('DOMContentLoaded', () => {
    if (!$('umBody')) return;
    $('umForm').addEventListener('submit', e => { e.preventDefault(); save(); });
    $('umAddBtn').addEventListener('click', () => openForm(null));
    $('umSaveBtn').addEventListener('click', save);
    $('umSearch').addEventListener('input', render);
    $('umFilterDistrict').addEventListener('change', render);
    // email, password and confirm password are checked when the field is left, then again on every key until valid
    ['umEmail', 'umPassword', 'umConfirmPassword'].forEach(id => {
      $(id).addEventListener('blur', () => { touched.add(id); checkLive(); });
      $(id).addEventListener('input', () => { if (touched.has(id)) checkLive(); });
    });
    $('umBody').addEventListener('click', e => {
      const btn = e.target.closest('[data-act]');
      const row = btn && rows.find(r => String(r.id) === btn.dataset.id);
      if (!row) return;
      if (btn.dataset.act === 'view') view(row);
      else if (btn.dataset.act === 'edit') openForm(row);
      else if (btn.dataset.act === 'delete') remove(row);
    });
    load();
  });
})();
