
        // ─── Role Selection ───
  // start from the server-rendered value so a failed post keeps the chosen role
  let selectedRole = (document.getElementById('selectedRole') || {}).value || 'admin';
  function selectRole(el, role) {
    document.querySelectorAll('.role-tab').forEach(t => t.classList.remove('active'));
  el.classList.add('active');
  selectedRole = role;
  const roleInput = document.getElementById('selectedRole');
  if (roleInput) roleInput.value = role;
        }

  // ─── Password Toggle ───
  function togglePassword() {
          const pw = document.getElementById('password');
  const icon = document.getElementById('pwIcon');
  const show = pw.type === 'password';
  pw.type = show ? 'text' : 'password';
  icon.classList.toggle('fa-eye', !show);
  icon.classList.toggle('fa-eye-slash', show);
        }

  // ─── Fill Demo Creds ───
  function fillCreds(user, pass, role) {
    document.getElementById('username').value = user;
  document.getElementById('password').value = pass;
  const tab = role ? document.querySelector('.role-tab[data-role="' + role + '"]') : null;
  if (tab) selectRole(tab, role);
  document.getElementById('loginError').classList.remove('visible');
        }

  // ─── Forgot Password ───
  function showForgot() {
    showToast('Reset Link', 'Password reset instructions sent to your registered email.', 'info');
        }

  // ─── MVC Login ───
  function startLogin() {
          const username = document.getElementById('username').value.trim();
  const password = document.getElementById('password').value;
  const btn = document.getElementById('loginBtn');

  if (username && password) {
    // ensure selected role is set on the hidden input before submit
    try {
      const roleInput = document.getElementById('selectedRole');
      if (roleInput) roleInput.value = selectedRole;
    } catch (e) { /* ignore */ }
    btn.classList.add('loading');
          }
        }

  // ─── Toast (standalone, no main.js dependency) ───
  function showToast(title, message, type) {
          const container = document.getElementById('toast-container');
  const icons = {success:'fa-circle-check', warning:'fa-triangle-exclamation', error:'fa-circle-xmark', info:'fa-circle-info' };
  const toast = document.createElement('div');
  toast.style.cssText = `min-width:280px;background:#fff;border-radius:10px;box-shadow:0 16px 40px rgba(0,0,0,.14);border:1px solid #d6dce8;padding:14px 16px;display:flex;align-items:flex-start;gap:12px;pointer-events:all;border-left:4px solid ${type === 'success' ? '#1b7a3e' : type === 'info' ? '#0369a1' : '#be123c'};animation:toastIn .3s ease`;
  toast.innerHTML = `<i class="fa-solid ${icons[type]||icons.info}" style="color:${type==='success'?'#1b7a3e':type==='info'?'#0369a1':'#be123c'};margin-top:2px"></i><div><div style="font-size:.8rem;font-weight:700;color:#1e293b">${title}</div>${message ? `<div style="font-size:.75rem;color:#64748b;margin-top:2px">${message}</div>` : ''}</div>`;
  container.appendChild(toast);
  const style = document.createElement('style');
  style.textContent = '@keyframes toastIn{from{opacity:0;transform:translateX(30px)}to{opacity:1;transform:none}}';
  document.head.appendChild(style);
          setTimeout(() => {toast.style.opacity = '0'; toast.style.transform = 'translateX(30px)'; toast.style.transition = 'all .3s ease'; setTimeout(()=>toast.remove(), 300); }, 4000);
        }

// Native form submission handles the Enter key.
