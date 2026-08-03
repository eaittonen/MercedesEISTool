const state = {
  currentRoute: null,
  currentFileId: null,
  activeResults: [],
  adminPage: 'dashboard',
  currentUser: null,
  adminData: {},
  selectedAdminUserId: null,
  adminStatusMessage: '',
  adminUserForm: {
    email: '',
    displayName: '',
    password: '',
    organizationId: '',
    roles: '',
    isEnabled: true,
    mustChangePassword: false
  }
};

const authStatus = document.getElementById('authStatus');
const searchInput = document.getElementById('searchInput');
const resultsHost = document.getElementById('resultsHost');
const detailHost = document.getElementById('detailHost');
const modal = document.getElementById('qrModal');
const qrBox = document.getElementById('qrBox');
const searchSummary = document.getElementById('searchSummary');
const signOutButton = document.getElementById('signOutButton');
const backButton = document.getElementById('backButton');
const searchButton = document.getElementById('searchButton');
const dataPanel = document.getElementById('dataPanel');
const installHint = document.getElementById('installHint');
const offlineBanner = document.getElementById('offlineBanner');

let debounceTimer = null;
let deferredPrompt = null;

function init() {
  bindEvents();
  updateOfflineStatus();
  window.addEventListener('online', updateOfflineStatus);
  window.addEventListener('offline', updateOfflineStatus);
  checkAuth().then((isAuthenticated) => {
    if (!isAuthenticated) {
      return;
    }

    const routeId = getRouteFileId();
    if (window.location.pathname.startsWith('/app/admin')) {
      showAdminView();
      loadAdminOverview();
    } else if (routeId) {
      state.currentFileId = routeId;
      showDetailView();
      loadFileDetails(routeId);
    } else {
      showSearchView();
      loadSearchResults('');
    }
  });
}

function bindEvents() {
  searchInput.addEventListener('input', () => {
    clearTimeout(debounceTimer);
    debounceTimer = setTimeout(() => loadSearchResults(searchInput.value.trim()), 300);
  });

  window.addEventListener('beforeinstallprompt', (event) => {
    event.preventDefault();
    deferredPrompt = event;
    if (installHint) {
      installHint.textContent = 'Install this page to keep a workshop-friendly shortcut on your device.';
    }
  });

  if (installHint) {
    installHint.addEventListener('click', async () => {
      if (!deferredPrompt) {
        installHint.textContent = 'This browser is not offering app installation right now.';
        return;
      }

      deferredPrompt.prompt();
      await deferredPrompt.userChoice;
      deferredPrompt = null;
      installHint.textContent = 'Installation prompt completed.';
    });
  }

  searchButton.addEventListener('click', () => {
    loadSearchResults(searchInput.value.trim());
  });

  signOutButton.addEventListener('click', async () => {
    await fetch('/auth/logout', { method: 'POST', credentials: 'same-origin' });
    window.location.assign('/login');
  });

  document.addEventListener('click', (event) => {
    const button = event.target.closest('[data-admin-page]');
    if (button) {
      event.preventDefault();
      state.adminPage = button.getAttribute('data-admin-page');
      renderAdminPage(state.adminPage);
      return;
    }

    const adminAction = event.target.closest('[data-admin-action]');
    if (adminAction) {
      event.preventDefault();
      handleAdminUserAction(adminAction.getAttribute('data-admin-action'));
      return;
    }

    const userRow = event.target.closest('[data-admin-user-id]');
    if (userRow) {
      event.preventDefault();
      selectAdminUser(userRow.getAttribute('data-admin-user-id'));
    }
  });

  backButton.addEventListener('click', () => {
    window.history.pushState({}, '', '/app');
    showSearchView();
    loadSearchResults(searchInput.value.trim());
  });
}

async function checkAuth() {
  try {
    const response = await fetch('/api/auth/me', { credentials: 'same-origin' });
    if (!response.ok) {
      if (response.status === 401) {
        throw new Error('not-authenticated');
      }
      throw new Error('auth-check-failed');
    }
    const user = await response.json();
    if (authStatus) {
      authStatus.textContent = `Signed in as ${user.displayName || user.email || 'user'}`;
    }
    return true;
  } catch (error) {
    if (error.message === 'not-authenticated') {
      window.location.assign('/login?returnUrl=' + encodeURIComponent(window.location.pathname + window.location.search));
      return false;
    }

    if (authStatus) {
      authStatus.textContent = 'Offline - authentication cannot currently be verified.';
    }
    return false;
  }
}

function getRouteFileId() {
  if (window.location.pathname.startsWith('/file/')) {
    return window.location.pathname.split('/').filter(Boolean).pop();
  }
  return null;
}

function showSearchView() {
  detailHost.classList.add('hidden');
  resultsHost.classList.remove('hidden');
  backButton.classList.add('hidden');
  dataPanel.innerHTML = '';
}

function showAdminView() {
  detailHost.classList.add('hidden');
  resultsHost.classList.add('hidden');
  backButton.classList.add('hidden');
  dataPanel.innerHTML = '<div class="empty-state">Loading administration workspace…</div>';
}

function renderAdminNavigation(activePage) {
  const pages = [
    { key: 'dashboard', title: 'Dashboard' },
    { key: 'users', title: 'Users' },
    { key: 'organizations', title: 'Organizations' },
    { key: 'sharing', title: 'Sharing' },
    { key: 'audit', title: 'Audit log' },
    { key: 'health', title: 'Health' },
    { key: 'releases', title: 'Releases' },
    { key: 'sessions', title: 'Sessions' },
    { key: 'vehicleCache', title: 'Vehicle cache' },
    { key: 'notifications', title: 'Notifications' },
    { key: 'flags', title: 'Feature flags' }
  ];

  return `
    <div class="admin-shell">
      <div class="admin-nav-grid">
        ${pages.map((page) => `<button class="admin-nav-btn ${activePage === page.key ? 'active' : ''}" data-admin-page="${page.key}">${escapeHtml(page.title)}</button>`).join('')}
      </div>
    </div>
  `;
}

function showDetailView() {
  resultsHost.classList.add('hidden');
  detailHost.classList.remove('hidden');
  backButton.classList.remove('hidden');
}

function updateOfflineStatus() {
  if (!offlineBanner) return;
  if (navigator.onLine) {
    offlineBanner.classList.add('hidden');
    return;
  }

  offlineBanner.classList.remove('hidden');
  offlineBanner.textContent = 'You are offline. The cached app shell is still available.';
}

async function loadAdminOverview() {
  try {
    const [meResponse, usersResponse, organizationsResponse, dashboardResponse, healthResponse, sharesResponse, auditResponse, sessionsResponse, releasesResponse, vehicleCacheResponse, notificationsResponse, featureFlagsResponse] = await Promise.all([
      fetch('/api/auth/me', { credentials: 'same-origin' }),
      fetch('/api/admin/users', { credentials: 'same-origin' }),
      fetch('/api/admin/organizations', { credentials: 'same-origin' }),
      fetch('/api/admin/dashboard', { credentials: 'same-origin' }),
      fetch('/api/admin/health', { credentials: 'same-origin' }),
      fetch('/api/admin/shares', { credentials: 'same-origin' }),
      fetch('/api/admin/audit-log', { credentials: 'same-origin' }),
      fetch('/api/admin/sessions', { credentials: 'same-origin' }),
      fetch('/api/admin/releases', { credentials: 'same-origin' }),
      fetch('/api/admin/vehicle-cache', { credentials: 'same-origin' }),
      fetch('/api/admin/notifications', { credentials: 'same-origin' }),
      fetch('/api/admin/feature-flags', { credentials: 'same-origin' })
    ]);

    if (!meResponse.ok || !usersResponse.ok || !organizationsResponse.ok || !dashboardResponse.ok || !healthResponse.ok || !sharesResponse.ok || !auditResponse.ok || !sessionsResponse.ok || !releasesResponse.ok || !vehicleCacheResponse.ok || !notificationsResponse.ok || !featureFlagsResponse.ok) {
      throw new Error('Unable to load administration workspace.');
    }

    const me = await meResponse.json();
    const users = await usersResponse.json();
    const organizations = await organizationsResponse.json();
    const dashboard = await dashboardResponse.json();
    const health = await healthResponse.json();
    const shares = await sharesResponse.json();
    const audit = await auditResponse.json();
    const sessions = await sessionsResponse.json();
    const releases = await releasesResponse.json();
    const vehicleCache = await vehicleCacheResponse.json();
    const notifications = await notificationsResponse.json();
    const featureFlags = await featureFlagsResponse.json();

    state.currentUser = me;
    state.adminData = { users, organizations, dashboard, health, shares, audit, sessions, releases, vehicleCache, notifications, featureFlags };
    renderAdminPage(state.adminPage);
  } catch (error) {
    dataPanel.innerHTML = `<div class="empty-state">${escapeHtml(error.message || 'Administration workspace unavailable.')}</div>`;
  }
}

function selectAdminUser(userId) {
  state.selectedAdminUserId = userId;
  const selectedUser = state.adminData.users?.items?.find((user) => user.id === userId);
  state.adminUserForm = {
    email: selectedUser?.email || '',
    displayName: selectedUser?.displayName || '',
    password: '',
    organizationId: selectedUser?.organizationId || '',
    roles: (selectedUser?.roles || []).join(', '),
    isEnabled: selectedUser?.isEnabled !== false,
    mustChangePassword: Boolean(selectedUser?.mustChangePassword)
  };
  state.adminStatusMessage = selectedUser ? `Selected ${selectedUser.email || selectedUser.displayName || 'user'}.` : 'No user selected.';
  renderAdminPage(state.adminPage);
}

function readAdminUserFormValues() {
  const email = document.querySelector('[data-admin-user-field="email"]')?.value || '';
  const displayName = document.querySelector('[data-admin-user-field="displayName"]')?.value || '';
  const password = document.querySelector('[data-admin-user-field="password"]')?.value || '';
  const organizationId = document.querySelector('[data-admin-user-field="organizationId"]')?.value || '';
  const rolesValue = document.querySelector('[data-admin-user-field="roles"]')?.value || '';
  const isEnabled = document.querySelector('[data-admin-user-field="isEnabled"]')?.checked ?? true;
  const mustChangePassword = document.querySelector('[data-admin-user-field="mustChangePassword"]')?.checked ?? false;

  return {
    email,
    displayName,
    password,
    organizationId,
    roles: rolesValue.split(',').map((role) => role.trim()).filter(Boolean),
    isEnabled,
    mustChangePassword
  };
}

async function handleAdminUserAction(action) {
  const selectedUser = state.adminData.users?.items?.find((user) => user.id === state.selectedAdminUserId);
  if (!selectedUser && action !== 'save-admin-user') {
    state.adminStatusMessage = 'Select a user before running that action.';
    renderAdminPage(state.adminPage);
    return;
  }

  if (action === 'save-admin-user') {
    if (!selectedUser) {
      state.adminStatusMessage = 'Select a user before saving changes.';
      renderAdminPage(state.adminPage);
      return;
    }

    const values = readAdminUserFormValues();
    const body = {
      email: values.email,
      displayName: values.displayName,
      password: values.password,
      organizationId: values.organizationId || selectedUser.organizationId || '',
      roles: values.roles.length ? values.roles : (selectedUser.roles || []),
      isEnabled: values.isEnabled,
      mustChangePassword: values.mustChangePassword
    };

    state.adminStatusMessage = 'Saving user changes…';
    renderAdminPage(state.adminPage);

    const response = await fetch(`/api/admin/users/${selectedUser.id}`, {
      method: 'PUT',
      headers: { 'Content-Type': 'application/json' },
      credentials: 'same-origin',
      body: JSON.stringify(body)
    });

    if (response.ok) {
      state.adminStatusMessage = 'User updated successfully.';
      await loadAdminOverview();
    } else {
      const errorPayload = await response.json().catch(() => ({}));
      state.adminStatusMessage = errorPayload.message || 'Unable to update the selected user.';
      renderAdminPage(state.adminPage);
    }
    return;
  }

  if (action === 'toggle-user-enabled') {
    const endpoint = selectedUser.isEnabled ? '/disable' : '/enable';
    const response = await fetch(`/api/admin/users/${selectedUser.id}${endpoint}`, { method: 'POST', credentials: 'same-origin' });
    if (response.ok) {
      state.adminStatusMessage = selectedUser.isEnabled ? 'User disabled.' : 'User enabled.';
      await loadAdminOverview();
    } else {
      const errorPayload = await response.json().catch(() => ({}));
      state.adminStatusMessage = errorPayload.message || 'Unable to change the user status.';
      renderAdminPage(state.adminPage);
    }
    return;
  }

  if (action === 'reset-password') {
    const newPassword = window.prompt('Enter a new password for the selected user');
    if (!newPassword) {
      state.adminStatusMessage = 'Password reset cancelled.';
      renderAdminPage(state.adminPage);
      return;
    }

    const response = await fetch(`/api/admin/users/${selectedUser.id}/reset-password`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      credentials: 'same-origin',
      body: JSON.stringify({ newPassword, forcePasswordChange: false })
    });

    if (response.ok) {
      state.adminStatusMessage = 'Password reset completed.';
      await loadAdminOverview();
    } else {
      const errorPayload = await response.json().catch(() => ({}));
      state.adminStatusMessage = errorPayload.message || 'Unable to reset the password.';
      renderAdminPage(state.adminPage);
    }
    return;
  }

  if (action === 'delete-user') {
    if (!window.confirm(`Delete ${selectedUser.email || selectedUser.displayName || 'the selected user'}?`)) {
      state.adminStatusMessage = 'Delete cancelled.';
      renderAdminPage(state.adminPage);
      return;
    }

    const response = await fetch(`/api/admin/users/${selectedUser.id}`, { method: 'DELETE', credentials: 'same-origin' });
    if (response.ok) {
      state.selectedAdminUserId = null;
      state.adminStatusMessage = 'User deleted.';
      await loadAdminOverview();
    } else {
      const errorPayload = await response.json().catch(() => ({}));
      state.adminStatusMessage = errorPayload.message || 'Unable to delete the user.';
      renderAdminPage(state.adminPage);
    }
  }
}

function renderAdminPage(page) {
  const adminData = state.adminData || {};
  const users = adminData.users?.items || [];
  const organizations = adminData.organizations?.items || [];
  const dashboard = adminData.dashboard || {};
  const health = adminData.health || {};
  const shares = adminData.shares || {};
  const audit = adminData.audit?.items || [];
  const sessions = adminData.sessions?.items || [];
  const releases = adminData.releases?.items || [];
  const vehicleCache = adminData.vehicleCache?.items || [];
  const notifications = adminData.notifications?.items || [];
  const featureFlags = adminData.featureFlags?.items || [];
  const selectedUser = state.selectedAdminUserId ? users.find((user) => user.id === state.selectedAdminUserId) : null;

  const pageContent = {
    dashboard: `
      <div class="admin-shell">
        ${renderAdminNavigation(page)}
        <div class="admin-grid">
          <div class="admin-card">
            <h3>Administration workspace</h3>
            <p>${escapeHtml(state.currentUser?.displayName || state.currentUser?.email || 'Administrator')} is managing ${users.length} user(s) and ${organizations.length} organization(s).</p>
            <div class="admin-inline-row">
              <span class="admin-pill">Server ${escapeHtml(dashboard.serverVersion || 'n/a')}</span>
              <span class="admin-pill">Status ${escapeHtml(dashboard.serverStatus || 'Healthy')}</span>
            </div>
          </div>
          <div class="admin-card">
            <h3>Runtime snapshot</h3>
            <div class="admin-stack">
              <div class="row"><span class="label">Uptime</span><span class="value">${escapeHtml(dashboard.uptime || 'n/a')}</span></div>
              <div class="row"><span class="label">DB size</span><span class="value">${escapeHtml(dashboard.databaseSize || 'n/a')}</span></div>
              <div class="row"><span class="label">Active sessions</span><span class="value">${escapeHtml(String(dashboard.activeSessions || 0))}</span></div>
              <div class="row"><span class="label">Queue length</span><span class="value">${escapeHtml(String(dashboard.queueLength || 0))}</span></div>
            </div>
          </div>
          <div class="admin-card">
            <h3>Quick actions</h3>
            <div class="actions">
              <button class="primary" data-admin-page="users">Users</button>
              <button data-admin-page="organizations">Organizations</button>
              <button data-admin-page="sharing">Sharing</button>
              <button data-admin-page="health">Health</button>
            </div>
          </div>
        </div>
      </div>
    `,
    users: `
      <div class="admin-shell">
        ${renderAdminNavigation(page)}
        <div class="admin-card">
          <h3>User management</h3>
          <div class="actions">
            <button class="primary" data-admin-action="save-admin-user">Save selected</button>
            <button data-admin-action="reset-password">Reset password</button>
            <button data-admin-action="toggle-user-enabled">Enable/disable</button>
            <button data-admin-action="delete-user">Delete user</button>
          </div>
          <div class="admin-inline-row">
            <span class="admin-pill">${escapeHtml(state.adminStatusMessage || 'Select a user to edit or manage their access.')}</span>
          </div>
          <div class="admin-grid">
            <div class="admin-card">
              <table class="admin-table">
                <thead><tr><th>Email</th><th>Organization</th><th>Roles</th><th>Status</th><th>Last login</th></tr></thead>
                <tbody>
                  ${users.length ? users.map((user) => `
                    <tr class="${selectedUser && selectedUser.id === user.id ? 'selected' : ''}" data-admin-user-id="${escapeHtml(String(user.id || ''))}">
                      <td>${escapeHtml(user.email || user.displayName || '—')}</td>
                      <td>${escapeHtml(user.organizationName || user.organizationId || '—')}</td>
                      <td>${escapeHtml((user.roles || []).join(', '))}</td>
                      <td>${escapeHtml(user.isEnabled ? 'Enabled' : 'Disabled')}</td>
                      <td>${escapeHtml(user.lastLoginAtUtc ? new Date(user.lastLoginAtUtc).toLocaleString() : '—')}</td>
                    </tr>
                  `).join('') : '<tr><td colspan="5">No users available</td></tr>'}
                </tbody>
              </table>
            </div>
            <div class="admin-card">
              <h4>${selectedUser ? 'Edit selected user' : 'Choose a user'}</h4>
              <div class="admin-form-grid">
                <label>Email
                  <input data-admin-user-field="email" value="${escapeHtml(selectedUser?.email || state.adminUserForm.email || '')}" />
                </label>
                <label>Display name
                  <input data-admin-user-field="displayName" value="${escapeHtml(selectedUser?.displayName || state.adminUserForm.displayName || '')}" />
                </label>
                <label>Password
                  <input data-admin-user-field="password" type="password" value="${escapeHtml(state.adminUserForm.password || '')}" />
                </label>
                <label>Organization ID
                  <input data-admin-user-field="organizationId" value="${escapeHtml(selectedUser?.organizationId || state.adminUserForm.organizationId || '')}" />
                </label>
                <label>Roles (comma-separated)
                  <input data-admin-user-field="roles" value="${escapeHtml((selectedUser?.roles || state.adminUserForm.roles || []).join(', '))}" />
                </label>
                <label><input data-admin-user-field="isEnabled" type="checkbox" ${selectedUser?.isEnabled === false ? '' : 'checked'} /> Enabled</label>
                <label><input data-admin-user-field="mustChangePassword" type="checkbox" ${selectedUser?.mustChangePassword ? 'checked' : ''} /> Force password change</label>
              </div>
            </div>
          </div>
        </div>
      </div>
    `,
    organizations: `
      <div class="admin-shell">
        ${renderAdminNavigation(page)}
        <div class="admin-card">
          <h3>Organization management</h3>
          <div class="actions">
            <button class="primary">Create organization</button>
            <button>Enable/disable</button>
          </div>
          <table class="admin-table">
            <thead><tr><th>Name</th><th>ID</th><th>Users</th><th>License</th></tr></thead>
            <tbody>
              ${organizations.length ? organizations.map((organization) => `
                <tr>
                  <td>${escapeHtml(organization.name || '—')}</td>
                  <td>${escapeHtml(organization.id || '—')}</td>
                  <td>${escapeHtml(String(organization.userCount || 0))}</td>
                  <td>${escapeHtml(organization.licenseType || '—')}</td>
                </tr>
              `).join('') : '<tr><td colspan="4">No organizations available</td></tr>'}
            </tbody>
          </table>
        </div>
      </div>
    `,
    sharing: `
      <div class="admin-shell">
        ${renderAdminNavigation(page)}
        <div class="admin-grid">
          <div class="admin-card">
            <h3>Share selected dump(s)</h3>
            <div class="admin-form-grid">
              <input placeholder="Organization" />
              <input placeholder="User (optional)" />
              <select><option>View metadata</option><option>View sensitive data</option><option>Download original</option><option>Edit metadata</option><option>Reanalyze</option><option>Share further</option></select>
              <input type="datetime-local" />
              <textarea placeholder="Notes"></textarea>
              <div class="actions"><button class="primary">Create share</button></div>
            </div>
          </div>
          <div class="admin-card">
            <h3>Incoming shares</h3>
            <div class="admin-list">${(shares.incomingShares || []).length ? (shares.incomingShares || []).map((item) => `<div class="admin-list-item"><strong>${escapeHtml(item.sourceOrganization || '—')}</strong><div>${escapeHtml(item.permissions || '—')}</div></div>`).join('') : '<div class="empty-state">No incoming shares</div>'}</div>
          </div>
          <div class="admin-card">
            <h3>Outgoing shares</h3>
            <div class="admin-list">${(shares.outgoingShares || []).length ? (shares.outgoingShares || []).map((item) => `<div class="admin-list-item"><strong>${escapeHtml(item.targetOrganization || item.targetUser || '—')}</strong><div>${escapeHtml(item.permissions || '—')}</div></div>`).join('') : '<div class="empty-state">No outgoing shares</div>'}</div>
          </div>
        </div>
      </div>
    `,
    audit: `
      <div class="admin-shell">
        ${renderAdminNavigation(page)}
        <div class="admin-card">
          <h3>Audit log</h3>
          <table class="admin-table">
            <thead><tr><th>Time</th><th>User</th><th>Action</th><th>Resource</th><th>IP</th></tr></thead>
            <tbody>
              ${audit.length ? audit.map((entry) => `
                <tr>
                  <td>${escapeHtml(new Date(entry.timestampUtc).toLocaleString())}</td>
                  <td>${escapeHtml(entry.user || '—')}</td>
                  <td>${escapeHtml(entry.action || '—')}</td>
                  <td>${escapeHtml(entry.resource || '—')}</td>
                  <td>${escapeHtml(entry.ipAddress || '—')}</td>
                </tr>
              `).join('') : '<tr><td colspan="5">No audit entries</td></tr>'}
            </tbody>
          </table>
        </div>
      </div>
    `,
    health: `
      <div class="admin-shell">
        ${renderAdminNavigation(page)}
        <div class="admin-grid">
          <div class="admin-card">
            <h3>System health</h3>
            <div class="admin-stack">
              <div class="row"><span class="label">CPU</span><span class="value">${escapeHtml(health.cpuUsage || 'n/a')}</span></div>
              <div class="row"><span class="label">RAM</span><span class="value">${escapeHtml(health.ramUsage || 'n/a')}</span></div>
              <div class="row"><span class="label">Disk</span><span class="value">${escapeHtml(health.diskUsage || 'n/a')}</span></div>
              <div class="row"><span class="label">SQLite</span><span class="value">${escapeHtml(health.sqliteStatus || 'n/a')}</span></div>
            </div>
          </div>
          <div class="admin-card">
            <h3>Services</h3>
            <div class="admin-list">${(health.backgroundServices || []).map((service) => `<div class="admin-list-item">${escapeHtml(service)}</div>`).join('')}</div>
          </div>
        </div>
      </div>
    `,
    releases: `
      <div class="admin-shell">
        ${renderAdminNavigation(page)}
        <div class="admin-card">
          <h3>Releases</h3>
          <table class="admin-table">
            <thead><tr><th>Version</th><th>Channel</th><th>Published</th><th>Mandatory</th><th>Downloads</th></tr></thead>
            <tbody>
              ${releases.length ? releases.map((release) => `
                <tr>
                  <td>${escapeHtml(release.version || '—')}</td>
                  <td>${escapeHtml(release.channel || '—')}</td>
                  <td>${escapeHtml(new Date(release.publishedUtc).toLocaleString())}</td>
                  <td>${escapeHtml(release.isMandatory ? 'Yes' : 'No')}</td>
                  <td>${escapeHtml(String(release.downloadCount || 0))}</td>
                </tr>
              `).join('') : '<tr><td colspan="5">No releases available</td></tr>'}
            </tbody>
          </table>
        </div>
      </div>
    `,
    sessions: `
      <div class="admin-shell">
        ${renderAdminNavigation(page)}
        <div class="admin-card">
          <h3>Active sessions</h3>
          <table class="admin-table">
            <thead><tr><th>User</th><th>Platform</th><th>Organization</th><th>Last seen</th></tr></thead>
            <tbody>
              ${sessions.length ? sessions.map((session) => `
                <tr>
                  <td>${escapeHtml(session.user || '—')}</td>
                  <td>${escapeHtml(session.platform || '—')}</td>
                  <td>${escapeHtml(session.organization || '—')}</td>
                  <td>${escapeHtml(new Date(session.lastSeenUtc).toLocaleString())}</td>
                </tr>
              `).join('') : '<tr><td colspan="4">No active sessions</td></tr>'}
            </tbody>
          </table>
        </div>
      </div>
    `,
    vehicleCache: `
      <div class="admin-shell">
        ${renderAdminNavigation(page)}
        <div class="admin-card">
          <h3>Vehicle lookup cache</h3>
          <table class="admin-table">
            <thead><tr><th>Registration</th><th>VIN</th><th>Cached</th><th>Expires</th><th>Provider</th></tr></thead>
            <tbody>
              ${vehicleCache.length ? vehicleCache.map((entry) => `
                <tr>
                  <td>${escapeHtml(entry.registration || '—')}</td>
                  <td>${escapeHtml(entry.vin || '—')}</td>
                  <td>${escapeHtml(new Date(entry.cachedUtc).toLocaleString())}</td>
                  <td>${escapeHtml(new Date(entry.expiresUtc).toLocaleString())}</td>
                  <td>${escapeHtml(entry.provider || '—')}</td>
                </tr>
              `).join('') : '<tr><td colspan="5">No cache entries</td></tr>'}
            </tbody>
          </table>
        </div>
      </div>
    `,
    notifications: `
      <div class="admin-shell">
        ${renderAdminNavigation(page)}
        <div class="admin-card">
          <h3>Notifications</h3>
          <div class="actions"><button class="primary">Send notification</button></div>
          <div class="admin-list">${notifications.length ? notifications.map((entry) => `<div class="admin-list-item"><strong>${escapeHtml(entry.title || '—')}</strong><div>${escapeHtml(entry.message || '—')}</div></div>`).join('') : '<div class="empty-state">No notifications</div>'}</div>
        </div>
      </div>
    `,
    flags: `
      <div class="admin-shell">
        ${renderAdminNavigation(page)}
        <div class="admin-card">
          <h3>Feature flags</h3>
          <div class="admin-list">${featureFlags.length ? featureFlags.map((entry) => `<div class="admin-list-item"><strong>${escapeHtml(entry.title || entry.key || '—')}</strong><div>${escapeHtml(entry.description || '—')}</div><div>${entry.enabled ? 'Enabled' : 'Disabled'}</div></div>`).join('') : '<div class="empty-state">No flags available</div>'}</div>
        </div>
      </div>
    `
  };

  dataPanel.innerHTML = pageContent[page] || pageContent.dashboard;
  dataPanel.querySelectorAll('[data-admin-page]').forEach((button) => {
    button.addEventListener('click', () => {
      state.adminPage = button.getAttribute('data-admin-page');
      renderAdminPage(state.adminPage);
    });
  });
}

async function loadSearchResults(term) {
  try {
    searchSummary.textContent = 'Searching…';
    const response = await fetch(`/api/files?search=${encodeURIComponent(term)}&page=1&pageSize=50`, { credentials: 'same-origin' });
    if (!response.ok) {
      throw new Error('Unable to load results');
    }
    const payload = await response.json();
    state.activeResults = payload.items || [];
    renderResults(payload);
  } catch (error) {
    searchSummary.textContent = error.message || 'Search failed.';
    resultsHost.innerHTML = '<div class="empty-state">Unable to load search results right now.</div>';
  }
}

function renderResults(payload) {
  const items = payload.items || [];
  const total = payload.totalCount || 0;
  const label = items.length === 0 ? 'No matches yet.' : `Showing first 50 of ${total} matches.`;
  searchSummary.textContent = label;
  if (items.length === 0) {
    resultsHost.innerHTML = '<div class="empty-state">No stored files matched that search.</div>';
    return;
  }

  const cards = items.map((item) => {
    const registration = item.registrationNumber || '—';
    const vin = item.detectedVin || item.userProvidedVin || '—';
    const customer = item.customerName || '—';
    const password = item.eisPassword || '—';
    const ssid = item.ssid || '—';
    const format = item.detectedFormat || 'Unknown';
    const uploaded = new Date(item.uploadedAtUtc).toLocaleString();
    return `
      <button class="result-card" data-id="${item.id}">
        <div class="badges">
          <span class="badge">${escapeHtml(registration)}</span>
          <span class="badge">${escapeHtml(format)}</span>
        </div>
        <h3>${escapeHtml(vin)}</h3>
        <div class="meta">Customer: ${escapeHtml(customer)}</div>
        <div class="meta">SSID: ${escapeHtml(ssid)}</div>
        <div class="meta">Password: ${escapeHtml(password)}</div>
        <div class="meta">Uploaded: ${escapeHtml(uploaded)}</div>
      </button>
    `;
  }).join('');

  resultsHost.innerHTML = `<div class="card-list">${cards}</div>`;
  resultsHost.querySelectorAll('.result-card').forEach((card) => {
    card.addEventListener('click', () => openFile(card.dataset.id));
  });
}

async function loadFileDetails(fileId) {
  try {
    const response = await fetch(`/api/files/${fileId}`, { credentials: 'same-origin' });
    if (!response.ok) {
      throw new Error('Unable to load file details');
    }
    const item = await response.json();
    renderDetail(item);
  } catch (error) {
    detailHost.innerHTML = `<div class="empty-state">${escapeHtml(error.message || 'Unable to load file details.')}</div>`;
  }
}

function renderDetail(item) {
  const detailHtml = `
    <div class="detail-grid">
      <div class="detail-card">
        <div class="row"><span class="label">VIN</span><span class="value">${escapeHtml(item.detectedVin || item.userProvidedVin || '—')}</span></div>
        <div class="row"><span class="label">Registration</span><span class="value">${escapeHtml(item.registrationNumber || '—')}</span></div>
        <div class="row"><span class="label">Customer</span><span class="value">${escapeHtml(item.customerName || '—')}</span></div>
        <div class="row"><span class="label">Additional info</span><span class="value">${escapeHtml(item.additionalInformation || '—')}</span></div>
        <div class="row"><span class="label">SSID</span><span class="value">${escapeHtml(item.ssid || '—')}</span></div>
        <div class="row"><span class="label">Password</span><span class="value">${escapeHtml(item.eisPassword || '—')}</span></div>
        <div class="row"><span class="label">Format</span><span class="value">${escapeHtml(item.detectedFormat || 'Unknown')}</span></div>
        <div class="row"><span class="label">Filename</span><span class="value">${escapeHtml(item.originalFileName || '—')}</span></div>
        <div class="row"><span class="label">Organization</span><span class="value">${escapeHtml(item.organizationName || '—')}</span></div>
        <div class="row"><span class="label">Uploaded</span><span class="value">${escapeHtml(new Date(item.uploadedAtUtc).toLocaleString())}</span></div>
        <div class="row"><span class="label">Initialized</span><span class="value">${renderBoolean(item.initialized)}</span></div>
        <div class="row"><span class="label">TP Cleared</span><span class="value">${renderBoolean(item.tpCleared)}</span></div>
        <div class="row"><span class="label">Personalized</span><span class="value">${renderBoolean(item.personalized)}</span></div>
        <div class="row"><span class="label">Activated</span><span class="value">${renderBoolean(item.activated)}</span></div>
        <div class="row"><span class="label">Warnings</span><span class="value">${escapeHtml(item.warnings || '—')}</span></div>
        <div class="row"><span class="label">Reason</span><span class="value">${escapeHtml(item.reason || '—')}</span></div>
      </div>
      <div class="detail-card">
        <div class="actions">
          <button class="primary" data-copy="password">Copy password</button>
          <button data-copy="vin">Copy VIN</button>
          <button data-copy="registration">Copy registration</button>
          <button data-copy="ssid">Copy SSID</button>
          <button data-copy="customer">Copy customer</button>
          <button data-copy="additional">Copy additional info</button>
          <button id="downloadButton">Download dump</button>
          <button id="qrButton">Open on mobile</button>
        </div>
      </div>
    </div>
  `;
  detailHost.innerHTML = detailHtml;
  detailHost.querySelectorAll('[data-copy]').forEach((button) => {
    button.addEventListener('click', () => copyValue(button.dataset.copy, item));
  });
  document.getElementById('downloadButton').addEventListener('click', () => downloadFile(item.id, item.originalFileName));
  document.getElementById('qrButton').addEventListener('click', () => showQr(item.id));
}

async function copyValue(kind, item) {
  const values = {
    vin: item.detectedVin || item.userProvidedVin || '',
    registration: item.registrationNumber || '',
    ssid: item.ssid || '',
    password: item.eisPassword || '',
    customer: item.customerName || '',
    additional: item.additionalInformation || ''
  };
  const value = values[kind] || '';
  if (!value) {
    alert('Nothing available to copy.');
    return;
  }
  await navigator.clipboard.writeText(value);
  alert(`${kind} copied.`);
}

function downloadFile(fileId, fileName) {
  const anchor = document.createElement('a');
  anchor.href = `/api/files/${fileId}/download`;
  anchor.download = fileName || 'download.bin';
  anchor.click();
}

function showQr(fileId) {
  const url = `${window.location.origin}/file/${fileId}`;
  qrBox.innerHTML = '<div>Loading QR…</div>';
  modal.classList.add('active');
  if (window.QRCode) {
    qrBox.innerHTML = '';
    new window.QRCode(qrBox, { text: url, width: 220, height: 220, colorDark: '#f3f7ff', colorLight: '#101b2d' });
  } else {
    qrBox.innerHTML = `<div>${escapeHtml(url)}</div>`;
  }
}

function openFile(fileId) {
  window.history.pushState({}, '', `/file/${fileId}`);
  state.currentFileId = fileId;
  showDetailView();
  loadFileDetails(fileId);
}

function renderBoolean(value) {
  if (value === true) return 'Yes';
  if (value === false) return 'No';
  return 'Unknown';
}

function escapeHtml(value) {
  return String(value ?? '')
    .replaceAll('&', '&amp;')
    .replaceAll('<', '&lt;')
    .replaceAll('>', '&gt;')
    .replaceAll('"', '&quot;')
    .replaceAll("'", '&#39;');
}

modal.addEventListener('click', (event) => {
  if (event.target === modal) {
    modal.classList.remove('active');
  }
});

document.addEventListener('DOMContentLoaded', init);
