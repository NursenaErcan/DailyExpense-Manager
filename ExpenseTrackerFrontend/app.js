const API_BASE = 'http://localhost:5000/api';

const CAT_COLORS = {
  Food:          '#c8a96e',
  Transport:     '#5b8de0',
  Shopping:      '#c06eb8',
  Health:        '#5ab87e',
  Bills:         '#e09b5a',
  Entertainment: '#e05a7a',
  Other:         '#6e8a9e'
};
const CAT_LABELS = {
  Food: 'Food & Drink', Transport: 'Transport', Shopping: 'Shopping',
  Health: 'Health', Bills: 'Bills & Utilities', Entertainment: 'Entertainment', Other: 'Other'
};

/* ── State ────────────────────────────────────────────────────────────────── */
let token = localStorage.getItem('et_token') || null;
let currentUser = JSON.parse(localStorage.getItem('et_user') || 'null');
let currentPage = 1;
let deleteTargetId = null;
let editTargetId = null;
let editReceiptDataUrl = null;
let editReceiptFileName = null;
let dailyChart = null, catChart = null, weeklyFullChart = null, monthlyFullChart = null, catFullChart = null;

/* ── API helper ───────────────────────────────────────────────────────────── */
async function api(path, options = {}) {
  const headers = { ...(options.headers || {}) };
  if (!(options.body instanceof FormData)) {
    headers['Content-Type'] = 'application/json';
  }
  if (token) headers['Authorization'] = `Bearer ${token}`;
  const res = await fetch(API_BASE + path, { headers, ...options });
  if (res.status === 401) { logout(); return null; }
  if (res.status === 204) return null;
  const data = await res.json().catch(() => null);
  if (!res.ok) throw data || { message: 'Request failed' };
  return data;
}

/* ── Auth ─────────────────────────────────────────────────────────────────── */
function showAuthTab(tab) {
  document.getElementById('login-form').style.display  = tab === 'login' ? '' : 'none';
  document.getElementById('register-form').style.display = tab === 'register' ? '' : 'none';
  document.getElementById('tab-login').classList.toggle('active', tab === 'login');
  document.getElementById('tab-register').classList.toggle('active', tab === 'register');
  document.getElementById('login-error').textContent = '';
  document.getElementById('register-error').textContent = '';
}

async function handleLogin(e) {
  e.preventDefault();
  const btn = document.getElementById('login-btn');
  const errEl = document.getElementById('login-error');
  errEl.textContent = '';
  setLoading(btn, true);
  try {
    const data = await api('/auth/login', {
      method: 'POST',
      body: JSON.stringify({
        email: document.getElementById('login-email').value,
        password: document.getElementById('login-password').value
      })
    });
    if (data) onAuthSuccess(data);
  } catch (err) {
    errEl.textContent = err?.message || 'Login failed. Check your credentials.';
  } finally { setLoading(btn, false); }
}

async function handleRegister(e) {
  e.preventDefault();
  const btn = document.getElementById('register-btn');
  const errEl = document.getElementById('register-error');
  errEl.textContent = '';
  setLoading(btn, true);
  try {
    const data = await api('/auth/register', {
      method: 'POST',
      body: JSON.stringify({
        fullName: document.getElementById('reg-name').value,
        email: document.getElementById('reg-email').value,
        password: document.getElementById('reg-password').value
      })
    });
    if (data) onAuthSuccess(data);
  } catch (err) {
    const msgs = err?.errors ? err.errors.join(' ') : (err?.message || 'Registration failed.');
    errEl.textContent = msgs;
  } finally { setLoading(btn, false); }
}

function onAuthSuccess(data) {
  token = data.token;
  currentUser = { email: data.email, fullName: data.fullName, isAdmin: data.isAdmin || false };
  localStorage.setItem('et_token', token);
  localStorage.setItem('et_user', JSON.stringify(currentUser));
  showApp();
}

function logout() {
  token = null; currentUser = null;
  localStorage.removeItem('et_token');
  localStorage.removeItem('et_user');
  document.getElementById('app').style.display = 'none';
  document.getElementById('auth-screen').style.display = 'flex';
}

/* ── App init ─────────────────────────────────────────────────────────────── */
function showApp() {
  document.getElementById('auth-screen').style.display = 'none';
  document.getElementById('app').style.display = 'flex';

  // Set user info in sidebar
  if (currentUser) {
    document.getElementById('user-name').textContent  = currentUser.fullName || 'User';
    document.getElementById('user-email').textContent = currentUser.email || '';
    document.getElementById('user-avatar').textContent = (currentUser.fullName || 'U')[0].toUpperCase();
    document.querySelectorAll('.admin-only').forEach(el => {
      el.style.display = currentUser?.isAdmin ? '' : 'none';
    });
  }

  // Set today's date on add form
  document.getElementById('a-date').value = new Date().toISOString().split('T')[0];

  // Set greeting
  const hr = new Date().getHours();
  const greeting = hr < 12 ? 'Good morning' : hr < 17 ? 'Good afternoon' : 'Good evening';
  document.getElementById('dash-greeting').textContent =
    `${greeting}, ${currentUser?.fullName?.split(' ')[0] || 'there'} 👋`;

  navigate('dashboard');
}

/* ── Navigation ───────────────────────────────────────────────────────────── */
function navigate(page) {
  if (page === 'admin' && !currentUser?.isAdmin) {
    showToast('Only admins can access this page.', 'error');
    page = 'dashboard';
  }

  document.querySelectorAll('.page').forEach(p => p.classList.remove('active'));
  document.querySelectorAll('.nav-item').forEach(n => {
    n.classList.toggle('active', n.dataset.page === page);
  });
  document.getElementById('page-' + page).classList.add('active');

  if (page === 'dashboard') loadDashboard();
  if (page === 'expenses')  { currentPage = 1; loadExpenses(); }
  if (page === 'charts')    loadCharts();
  if (page === 'budget')    loadBudgets();
  if (page === 'admin')     loadAdmin();
}

/* ── Dashboard ────────────────────────────────────────────────────────────── */
async function loadDashboard() {
  try {
    const summary = await api('/expenses/summary');
    if (!summary) return;

    document.getElementById('m-today').textContent = fmt(summary.todayTotal);
    document.getElementById('m-week').textContent  = fmt(summary.weekTotal);
    document.getElementById('m-month').textContent = fmt(summary.monthTotal);

    buildDailyChart(summary.last7Days);
    buildCatChart(summary.byCategory, 'chart-cat', 'cat-legend');
    buildRecentList(summary);
  } catch (e) { showToast('Failed to load dashboard', 'error'); }
}

function buildDailyChart(data) {
  if (dailyChart) dailyChart.destroy();
  const labels = data.map(d => {
    const dt = new Date(d.date); return dt.toLocaleDateString('en-GB', { weekday: 'short', day: 'numeric' });
  });
  const values = data.map(d => parseFloat(d.total.toFixed(2)));
  dailyChart = new Chart(document.getElementById('chart-daily'), {
    type: 'bar',
    data: {
      labels,
      datasets: [{
        data: values,
        backgroundColor: 'rgba(200,169,110,0.7)',
        borderColor: '#c8a96e',
        borderWidth: 1,
        borderRadius: 5,
        borderSkipped: false
      }]
    },
    options: {
      responsive: true, maintainAspectRatio: false,
      plugins: { legend: { display: false }, tooltip: { callbacks: { label: c => fmt(c.raw) } } },
      scales: {
        y: { beginAtZero: true, ticks: { color: '#5a5856', callback: v => '₺' + v }, grid: { color: 'rgba(255,255,255,0.04)' } },
        x: { ticks: { color: '#5a5856' }, grid: { display: false }, ticks: { autoSkip: false, maxRotation: 30 } }
      }
    }
  });
}

function buildCatChart(data, canvasId, legendId) {
  const el = document.getElementById(canvasId);
  if (!el) return;
  const existing = Chart.getChart(el);
  if (existing) existing.destroy();

  if (!data || !data.length) {
    document.getElementById(legendId).innerHTML = '<span style="color:var(--text3);font-size:0.82rem">No data yet</span>';
    return;
  }
  const cats = data.map(d => d.category);
  const vals = data.map(d => parseFloat(d.total.toFixed(2)));
  const colors = cats.map(c => CAT_COLORS[c] || '#6e8a9e');

  new Chart(el, {
    type: 'doughnut',
    data: { labels: cats.map(c => CAT_LABELS[c] || c), datasets: [{ data: vals, backgroundColor: colors, borderWidth: 2, borderColor: '#16181c' }] },
    options: {
      responsive: true, maintainAspectRatio: false, cutout: '65%',
      plugins: { legend: { display: false }, tooltip: { callbacks: { label: c => c.label + ': ' + fmt(c.raw) } } }
    }
  });

  document.getElementById(legendId).innerHTML = data.map(d =>
    `<span class="legend-item"><span class="legend-dot" style="background:${CAT_COLORS[d.category]||'#6e8a9e'}"></span>${CAT_LABELS[d.category]||d.category}: ${fmt(d.total)}</span>`
  ).join('');
}

async function buildRecentList() {
  const result = await api('/expenses?pageSize=5&page=1');
  const el = document.getElementById('recent-list');
  if (!result || !result.items.length) {
    el.innerHTML = '<div class="empty-state"><p>No expenses recorded yet.</p></div>';
    return;
  }
  el.innerHTML = result.items.map(e => `
    <div class="recent-item">
      <div class="recent-cat-dot" style="background:${CAT_COLORS[e.category]||'#6e8a9e'}"></div>
      <div class="recent-info">
        <div class="recent-name">${escHtml(e.description)}</div>
        <div class="recent-meta">${CAT_LABELS[e.category]||e.category} · ${fmtDate(e.date)}</div>
      </div>
      <div class="recent-amount">${fmt(e.amount)}</div>
    </div>`).join('');
}

/* ── Expenses List ────────────────────────────────────────────────────────── */
async function loadExpenses() {
  const cat    = document.getElementById('filter-cat').value;
  const period = document.getElementById('filter-period').value;
  const from   = document.getElementById('filter-from').value;
  const to     = document.getElementById('filter-to').value;

  let qs = `?page=${currentPage}&pageSize=15`;
  if (cat)    qs += `&category=${cat}`;
  if (period) qs += `&period=${period}`;
  if (from)   qs += `&from=${from}`;
  if (to)     qs += `&to=${to}`;

  try {
    const result = await api('/expenses' + qs);
    if (!result) return;

    document.getElementById('expense-count').textContent =
      `${result.totalCount} expense${result.totalCount !== 1 ? 's' : ''} found`;

    const tbody = document.getElementById('expense-tbody');
    if (!result.items.length) {
      tbody.innerHTML = `<tr><td colspan="7"><div class="empty-state"><p>No expenses found for the selected filters.</p></div></td></tr>`;
      document.getElementById('pagination').innerHTML = '';
      return;
    }

    tbody.innerHTML = result.items.map(e => `
      <tr>
        <td>${escHtml(e.description)}</td>
        <td><span class="cat-badge" style="background:${CAT_COLORS[e.category]||'#6e8a9e'}20;color:${CAT_COLORS[e.category]||'#6e8a9e'}">
          <span style="width:6px;height:6px;border-radius:50%;background:currentColor;display:inline-block"></span>
          ${CAT_LABELS[e.category]||e.category}
        </span></td>
        <td>${fmtDate(e.date)}</td>
        <td class="amount-cell">${fmt(e.amount)}</td>
        <td class="note-cell" title="${escHtml(e.note||'')}">${escHtml(e.note || '—')}</td>
        <td>${e.receiptImageDataUrl ? `<a class="receipt-link" href="${e.receiptImageDataUrl}" target="_blank" rel="noopener">View</a>` : "—"}</td>
        <td class="actions-cell">
          <button class="tbl-btn" onclick="openEditPanel(${e.id})">Edit</button>
          <button class="tbl-btn del" onclick="askDelete(${e.id})">Delete</button>
        </td>
      </tr>`).join('');

    buildPagination(result.totalPages, result.page);
  } catch (e) { showToast('Failed to load expenses', 'error'); }
}

function clearFilters() {
  document.getElementById('filter-cat').value = '';
  document.getElementById('filter-period').value = '';
  document.getElementById('filter-from').value = '';
  document.getElementById('filter-to').value = '';
  currentPage = 1;
  loadExpenses();
}

function buildPagination(totalPages, page) {
  const el = document.getElementById('pagination');
  if (totalPages <= 1) { el.innerHTML = ''; return; }
  let html = `<button class="page-btn" onclick="goPage(${page-1})" ${page<=1?'disabled':''}>←</button>`;
  for (let i = 1; i <= totalPages; i++) {
    html += `<button class="page-btn ${i===page?'active':''}" onclick="goPage(${i})">${i}</button>`;
  }
  html += `<button class="page-btn" onclick="goPage(${page+1})" ${page>=totalPages?'disabled':''}>→</button>`;
  el.innerHTML = html;
}

function goPage(p) { currentPage = p; loadExpenses(); }

/* ── Edit ─────────────────────────────────────────────────────────────────── */
async function openEditPanel(id) {
  try {
    const e = await api(`/expenses/${id}`);
    if (!e) return;
    editTargetId = id;
    document.getElementById('e-desc').value   = e.description;
    document.getElementById('e-amount').value = e.amount;
    document.getElementById('e-cat').value    = e.category;
    document.getElementById('e-date').value   = e.date;
    document.getElementById('e-note').value   = e.note || '';
    editReceiptDataUrl = e.receiptImageDataUrl || null;
    editReceiptFileName = e.receiptFileName || null;
    document.getElementById('e-receipt').value = '';
    document.getElementById('e-current-receipt').innerHTML = editReceiptDataUrl ? `<a class="receipt-link" href="${editReceiptDataUrl}" target="_blank" rel="noopener">Current receipt: ${escHtml(editReceiptFileName || 'View image')}</a>` : 'No receipt uploaded.';
    document.getElementById('edit-error').textContent = '';
    const panel = document.getElementById('edit-panel');
    panel.style.display = '';
    panel.scrollIntoView({ behavior: 'smooth', block: 'start' });
  } catch { showToast('Could not load expense', 'error'); }
}

function closeEditPanel() {
  document.getElementById('edit-panel').style.display = 'none';
  editTargetId = null;
  editReceiptDataUrl = null;
  editReceiptFileName = null;
}

async function saveEdit() {
  if (!editTargetId) return;
  const errEl = document.getElementById('edit-error');
  errEl.textContent = '';
  try {
    const editReceipt = await readReceiptFile('e-receipt');
    await api(`/expenses/${editTargetId}`, {
      method: 'PUT',
      body: JSON.stringify({
        description: document.getElementById('e-desc').value,
        amount:      parseFloat(document.getElementById('e-amount').value),
        category:    document.getElementById('e-cat').value,
        date:        document.getElementById('e-date').value,
        note:        document.getElementById('e-note').value,
        receiptImageDataUrl: editReceipt.dataUrl || editReceiptDataUrl,
        receiptFileName: editReceipt.fileName || editReceiptFileName
      })
    });
    closeEditPanel();
    loadExpenses();
    showToast('Expense updated', 'success');
  } catch (e) { errEl.textContent = e?.message || 'Update failed.'; }
}

/* ── Delete ───────────────────────────────────────────────────────────────── */
function askDelete(id) {
  deleteTargetId = id;
  document.getElementById('modal-overlay').style.display = 'flex';
}
function closeModal() {
  deleteTargetId = null;
  document.getElementById('modal-overlay').style.display = 'none';
}
async function confirmDelete() {
  if (!deleteTargetId) return;
  try {
    await api(`/expenses/${deleteTargetId}`, { method: 'DELETE' });
    closeModal();
    loadExpenses();
    showToast('Expense deleted', 'success');
  } catch { showToast('Delete failed', 'error'); }
}

/* ── Add Expense ──────────────────────────────────────────────────────────── */
async function handleAddExpense(e) {
  e.preventDefault();
  const btn = document.getElementById('add-btn');
  const errEl = document.getElementById('add-error');
  const sucEl = document.getElementById('add-success');
  errEl.textContent = ''; sucEl.textContent = '';
  setLoading(btn, true);
  try {
    const formData = new FormData();
    formData.append('Description', document.getElementById('a-desc').value);
    formData.append('Amount', document.getElementById('a-amount').value);
    formData.append('Category', document.getElementById('a-cat').value);
    formData.append('Date', document.getElementById('a-date').value);
    formData.append('Note', document.getElementById('a-note').value || '');

    const receiptInput = document.getElementById('a-receipt');
    const file = receiptInput?.files?.[0];
    if (file) {
      if (!file.type.startsWith('image/')) throw { message: 'Receipt must be an image file.' };
      if (file.size > 2 * 1024 * 1024) throw { message: 'Receipt image must be smaller than 2 MB.' };
      formData.append('receipt', file);
    }

    await api('/expenses', {
      method: 'POST',
      body: formData
    });

    sucEl.textContent = 'Expense added successfully!';
    document.getElementById('a-desc').value = '';
    document.getElementById('a-amount').value = '';
    document.getElementById('a-note').value = '';
    document.getElementById('a-receipt').value = '';
    setTimeout(() => sucEl.textContent = '', 3000);
    showToast('Expense added!', 'success');
  } catch (err) {
    errEl.textContent = err?.message || 'Failed to add expense.';
  } finally { setLoading(btn, false); }
}

function resetAddForm() {
  document.getElementById('a-desc').value = '';
  document.getElementById('a-amount').value = '';
  document.getElementById('a-note').value = '';
    document.getElementById('a-receipt').value = '';
  document.getElementById('a-date').value = new Date().toISOString().split('T')[0];
  document.getElementById('add-error').textContent = '';
  document.getElementById('add-success').textContent = '';
}

/* ── Charts Page ──────────────────────────────────────────────────────────── */
async function loadCharts() {
  try {
    const summary = await api('/expenses/summary');
    if (!summary) return;

    // Weekly chart
    if (weeklyFullChart) weeklyFullChart.destroy();
    const wLabels = summary.last7Days.map(d => {
      const dt = new Date(d.date);
      return dt.toLocaleDateString('en-GB', { weekday: 'short', day: 'numeric' });
    });
    weeklyFullChart = new Chart(document.getElementById('chart-weekly-full'), {
      type: 'bar',
      data: {
        labels: wLabels,
        datasets: [{
          label: 'Daily spending',
          data: summary.last7Days.map(d => parseFloat(d.total.toFixed(2))),
          backgroundColor: 'rgba(200,169,110,0.7)',
          borderColor: '#c8a96e',
          borderWidth: 1,
          borderRadius: 6,
          borderSkipped: false
        }]
      },
      options: {
        responsive: true, maintainAspectRatio: false,
        plugins: { legend: { display: false }, tooltip: { callbacks: { label: c => fmt(c.raw) } } },
        scales: {
          y: { beginAtZero: true, ticks: { color: '#5a5856', callback: v => '₺' + v.toLocaleString() }, grid: { color: 'rgba(255,255,255,0.04)' } },
          x: { ticks: { color: '#5a5856', autoSkip: false }, grid: { display: false } }
        }
      }
    });

    // Monthly chart
    if (monthlyFullChart) monthlyFullChart.destroy();
    monthlyFullChart = new Chart(document.getElementById('chart-monthly-full'), {
      type: 'bar',
      data: {
        labels: summary.last6Months.map(m => m.monthName + ' ' + m.year),
        datasets: [{
          label: 'Monthly spending',
          data: summary.last6Months.map(m => parseFloat(m.total.toFixed(2))),
          backgroundColor: 'rgba(91,141,224,0.7)',
          borderColor: '#5b8de0',
          borderWidth: 1,
          borderRadius: 6,
          borderSkipped: false
        }]
      },
      options: {
        responsive: true, maintainAspectRatio: false,
        plugins: { legend: { display: false }, tooltip: { callbacks: { label: c => fmt(c.raw) } } },
        scales: {
          y: { beginAtZero: true, ticks: { color: '#5a5856', callback: v => '₺' + v.toLocaleString() }, grid: { color: 'rgba(255,255,255,0.04)' } },
          x: { ticks: { color: '#5a5856', autoSkip: false }, grid: { display: false } }
        }
      }
    });

    // Category doughnut (full page)
    buildCatChart(summary.byCategory, 'chart-cat-full', 'cat-legend-full');

  } catch { showToast('Failed to load charts', 'error'); }
}

/* Receipt helpers */
function readReceiptFile(inputId) {
  const input = document.getElementById(inputId);
  const file = input?.files?.[0];
  if (!file) return Promise.resolve({ dataUrl: null, fileName: null });
  if (!file.type.startsWith('image/')) return Promise.reject({ message: 'Receipt must be an image file.' });
  if (file.size > 2 * 1024 * 1024) return Promise.reject({ message: 'Receipt image must be smaller than 2 MB.' });

  return new Promise((resolve, reject) => {
    const reader = new FileReader();
    reader.onload = () => resolve({ dataUrl: reader.result, fileName: file.name });
    reader.onerror = () => reject({ message: 'Could not read receipt image.' });
    reader.readAsDataURL(file);
  });
}

/* Admin  */
async function loadAdmin() {
  try {
    const users = await api('/admin/users');
    const backup = await api('/admin/backup');
    const alerts = await api('/admin/alerts');

    if (!users || !backup || !alerts) return;

    const expenses = backup.expenses || backup.Expenses || [];

    // Metrics
    document.getElementById('admin-users-total').textContent = users.length;
    document.getElementById('admin-expenses-total').textContent = expenses.length;
    document.getElementById('admin-receipts-total').textContent =
      expenses.filter(e => e.receiptPath || e.ReceiptPath).length;
    document.getElementById('admin-alerts-total').textContent = alerts.length;

    // USERS LIST (THIS IS YOUR "MANAGE USERS")
    document.getElementById('admin-users-list').innerHTML = users.map(u => `
      <div class="admin-row">
        <div>
          <strong>${escHtml(u.userName || u.UserName)}</strong><br>
          <span>${escHtml(u.email || u.Email)}</span>
        </div>
        <button class="tbl-btn del" onclick="deleteAdminUser('${u.id || u.Id}')">
          Delete
        </button>
      </div>
    `).join('');

    // ALERTS LIST
    document.getElementById('admin-alerts-list').innerHTML = alerts.length ? alerts.map(a => `
      <div class="admin-row">
        <div>
          <strong>${escHtml(a.userName)}</strong> — ${a.month}/${a.year}<br>
          <span>${escHtml(a.message)}</span>
        </div>
        <div class="amount-cell">
          ${fmt(a.total)} / ${fmt(a.limit)}
        </div>
      </div>
    `).join('') : '<div class="empty-state"><p>No budget alerts yet.</p></div>';

    // EXPENSES LIST
    document.getElementById('admin-expenses-list').innerHTML = expenses.map(e => `
      <div class="admin-row">
        <div>
          <strong>${escHtml(e.description || e.Description)}</strong><br>
          <span>${escHtml(e.category || e.Category)}</span>
        </div>
        <div class="amount-cell">${fmt(e.amount || e.Amount)}</div>
      </div>
    `).join('');

  } catch (e) {
    console.error(e);
    showToast('Failed to load admin dashboard', 'error');
  }
}

async function deleteAdminUser(id) {
  if (!confirm('Delete this user?')) return;

  try {
    await api(`/admin/users/${id}`, { method: 'DELETE' });
    showToast('User deleted', 'success');
    loadAdmin();
  } catch (e) {
    showToast(e?.message || 'Could not delete user', 'error');
  }
}

async function downloadBackup() {
  try {
    const backup = await api('/admin/backup');

    const json = JSON.stringify(backup, null, 2);
    document.getElementById('backup-preview').textContent =
      json.slice(0, 900) + (json.length > 900 ? '\n...' : '');

    const blob = new Blob([json], { type: 'application/json' });
    const url = URL.createObjectURL(blob);

    const a = document.createElement('a');
    a.href = url;
    a.download = `expense-tracker-backup-${new Date().toISOString().slice(0, 10)}.json`;
    a.click();

    URL.revokeObjectURL(url);
    showToast('Backup downloaded', 'success');

  } catch {
    showToast('Backup failed', 'error');
  }
}

async function sendNotification() {
  const err = document.getElementById('notif-error');
  err.textContent = '';

  const title = document.getElementById('notif-title').value;
  const body = document.getElementById('notif-message').value;

  if (!title || !body) {
    err.textContent = 'Please enter title and message.';
    return;
  }

  try {
    await api('/admin/send-notification', {
      method: 'POST',
      body: JSON.stringify({ title, body })
    });

    showToast('Notification sent', 'success');

    document.getElementById('notif-title').value = '';
    document.getElementById('notif-message').value = '';

  } catch (e) {
    err.textContent = e?.message || 'Notification failed.';
  }
}
/* Utilities  */
function fmt(n) {
  return '₺' + parseFloat(n || 0).toLocaleString('tr-TR', { minimumFractionDigits: 2, maximumFractionDigits: 2 });
}

function fmtDate(dateStr) {
  if (!dateStr) return '';
  const d = new Date(dateStr + (dateStr.includes('T') ? '' : 'T00:00'));
  return d.toLocaleDateString('en-GB', { day: 'numeric', month: 'short', year: 'numeric' });
}

function escHtml(str) {
  if (!str) return '';
  return str.replace(/&/g,'&amp;').replace(/</g,'&lt;').replace(/>/g,'&gt;').replace(/"/g,'&quot;');
}

function setLoading(btn, loading) {
  btn.querySelector('.btn-text').style.display = loading ? 'none' : '';
  btn.querySelector('.btn-loader').style.display = loading ? '' : 'none';
  btn.disabled = loading;
}

let toastTimer;
function showToast(msg, type = '') {
  const el = document.getElementById('toast');
  el.textContent = msg;
  el.className = 'toast show ' + type;
  clearTimeout(toastTimer);
  toastTimer = setTimeout(() => el.className = 'toast', 3000);
}
/* ── Budget ───────────────────────────────────────────────────────────────── */
let currentBudgetMonth = new Date().getMonth() + 1;
let currentBudgetYear = new Date().getFullYear();

async function loadBudgets() {
  try {
    const monthPickerEl = document.getElementById('budget-month-picker');
    const now = new Date();
    
    if (monthPickerEl && monthPickerEl.value === 'next') {
      const nextMonth = new Date(now.getFullYear(), now.getMonth() + 1, 1);
      currentBudgetMonth = nextMonth.getMonth() + 1;
      currentBudgetYear = nextMonth.getFullYear();
    } else {
      currentBudgetMonth = now.getMonth() + 1;
      currentBudgetYear = now.getFullYear();
    }

    const budgets = await api(`/budgets?month=${currentBudgetMonth}&year=${currentBudgetYear}`);
    if (!budgets) {
      showToast('Failed to load budgets', 'error');
      return;
    }

    const list = document.getElementById('budget-list');
    
    if (budgets.length === 0) {
      list.innerHTML = '<div class="empty-state"><p>No budgets set for this month. Create one to get started!</p></div>';
      return;
    }

    list.innerHTML = budgets.map(b => {
      const percentage = Math.min((b.spent / b.limit) * 100, 100);
      const isWarning = percentage >= 80 && percentage < 100;
      const isDanger = percentage >= 100;
      
      return `
        <div class="budget-card">
          <div class="budget-info">
            <div class="budget-category">${CAT_LABELS[b.category] || b.category}</div>
            <div class="budget-bar">
              <div class="budget-bar-fill ${isDanger ? 'danger' : isWarning ? 'warning' : ''}" style="width: ${percentage}%"></div>
            </div>
            <div class="budget-status">
              <span>₺${b.spent.toFixed(2)} / ₺${b.limit.toFixed(2)}</span>
              <span>${percentage.toFixed(0)}%</span>
            </div>
          </div>
          <div class="budget-actions">
            <button title="Edit" onclick="editBudget(${b.id}, '${b.category}', ${b.limit})">✎</button>
            <button title="Delete" onclick="deleteBudget(${b.id})">✕</button>
          </div>
        </div>
      `;
    }).join('');
  } catch (e) {
    console.error(e);
    showToast('Failed to load budgets', 'error');
  }
}

function openAddBudgetModal() {
  document.getElementById('budget-modal-title').textContent = 'Add Budget';
  document.getElementById('budget-category').value = '';
  document.getElementById('budget-limit').value = '';
  document.getElementById('budget-error').textContent = '';
  document.getElementById('budget-modal').dataset.budgetId = '';
  document.getElementById('budget-modal').style.display = 'block';
  document.getElementById('budget-modal-backdrop').style.display = 'flex';
}

function editBudget(id, category, limit) {
  document.getElementById('budget-modal-title').textContent = 'Edit Budget';
  document.getElementById('budget-category').value = category;
  document.getElementById('budget-limit').value = limit;
  document.getElementById('budget-error').textContent = '';
  document.getElementById('budget-modal').dataset.budgetId = id;
  document.getElementById('budget-category').disabled = true;
  document.getElementById('budget-modal').style.display = 'block';
  document.getElementById('budget-modal-backdrop').style.display = 'flex';
}

function closeBudgetModal() {
  document.getElementById('budget-modal').style.display = 'none';
  document.getElementById('budget-modal-backdrop').style.display = 'none';
  document.getElementById('budget-category').disabled = false;
}

async function handleSaveBudget(e) {
  console.log('handleSaveBudget called');
  e.preventDefault();
  e.stopPropagation();
  
  const budgetModal = document.getElementById('budget-modal');
  const budgetId = budgetModal.dataset.budgetId;
  const category = document.getElementById('budget-category').value;
  const limitInput = document.getElementById('budget-limit').value;
  const limit = parseFloat(limitInput);
  const errEl = document.getElementById('budget-error');
  
  console.log('Form data:', { budgetId, category, limit });
  
  errEl.textContent = '';
  
  if (!category || !limit || isNaN(limit)) {
    errEl.textContent = 'Please fill in all fields correctly';
    console.log('Validation failed');
    return false;
  }

  try {
    let response;
    
    if (budgetId) {
      console.log('Updating budget:', budgetId);
      response = await api(`/budgets/${budgetId}`, {
        method: 'PUT',
        body: JSON.stringify({ limit })
      });
    } else {
      console.log('Creating new budget');
      response = await api('/budgets', {
        method: 'POST',
        body: JSON.stringify({
          category,
          limit,
          month: currentBudgetMonth,
          year: currentBudgetYear
        })
      });
    }
    
    console.log('API response:', response);
    
    if (response) {
      console.log('Budget saved successfully');
      const message = budgetId ? 'Budget updated' : 'Budget created';
      showToast(message, 'success');
      
      // Stay on budget page, refresh list
      closeBudgetModal();
      document.getElementById('budget-form').reset();
      await loadBudgets();
      navigate('budget');
      console.log('Budgets loaded');
    } else {
      throw new Error('Server returned no data');
    }
  } catch (err) {
    console.error('Error saving budget:', err);
    const message = err?.message || err?.errors?.[0] || 'Failed to save budget. Check your connection and try again.';
    errEl.textContent = message;
    showToast(message, 'error');
  }
  
  return false;
}

async function deleteBudget(id) {
  if (!confirm('Delete this budget?')) return;

  try {
    await api(`/budgets/${id}`, { method: 'DELETE' });
    showToast('Budget deleted', 'success');
    loadBudgets();
  } catch (e) {
    showToast(e?.message || 'Failed to delete budget', 'error');
  }
}

// Close modal when clicking outside of it
window.addEventListener('click', function(event) {
  const modal = document.getElementById('budget-modal');
  const backdrop = document.getElementById('budget-modal-backdrop');
  if (event.target === backdrop) {
    closeBudgetModal();
  }
});

const budgetForm = document.getElementById('budget-form');
if (budgetForm) {
  budgetForm.addEventListener('submit', handleSaveBudget);
}
/* ── Boot ─────────────────────────────────────────────────────────────────── */
if (token && currentUser) {
  showApp();
} else {
  document.getElementById('auth-screen').style.display = 'flex';
  document.getElementById('app').style.display = 'none';
}
