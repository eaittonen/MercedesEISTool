const state = {
  currentRoute: null,
  currentFileId: null,
  activeResults: []
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
  checkAuth().then(() => {
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
      throw new Error('not-authenticated');
    }
    const user = await response.json();
    if (authStatus) {
      authStatus.textContent = `Signed in as ${user.displayName || user.email || 'user'}`;
    }
    return true;
  } catch {
    window.location.assign('/login?returnUrl=' + encodeURIComponent(window.location.pathname + window.location.search));
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
    const [meResponse, usersResponse, organizationsResponse, healthResponse] = await Promise.all([
      fetch('/api/auth/me', { credentials: 'same-origin' }),
      fetch('/api/admin/users', { credentials: 'same-origin' }),
      fetch('/api/admin/organizations', { credentials: 'same-origin' }),
      fetch('/api/health', { credentials: 'same-origin' })
    ]);

    if (!meResponse.ok || !usersResponse.ok || !organizationsResponse.ok || !healthResponse.ok) {
      throw new Error('Unable to load administration workspace.');
    }

    const me = await meResponse.json();
    const users = await usersResponse.json();
    const organizations = await organizationsResponse.json();
    const health = await healthResponse.json();

    const userCount = users.items?.length || 0;
    const organizationCount = organizations.items?.length || 0;
    const healthBadge = health.isHealthy ? 'Healthy' : 'Attention';
    const adminSectionHtml = `
      <div class="detail-grid">
        <div class="detail-card">
          <h3>Administration workspace</h3>
          <p>${escapeHtml(me.displayName || me.email || 'Administrator')} is managing ${userCount} user(s) and ${organizationCount} organization(s).</p>
          <div class="actions">
            <button class="primary">Users</button>
            <button>Organizations</button>
            <button>Sharing</button>
            <button>Health</button>
          </div>
        </div>
        <div class="detail-card">
          <div class="row"><span class="label">System health</span><span class="value">${escapeHtml(healthBadge)}</span></div>
          <div class="row"><span class="label">Server version</span><span class="value">${escapeHtml(health.serverVersion || 'n/a')}</span></div>
          <div class="row"><span class="label">Service</span><span class="value">${escapeHtml(health.serviceName || 'n/a')}</span></div>
        </div>
      </div>
    `;
    dataPanel.innerHTML = adminSectionHtml;
  } catch (error) {
    dataPanel.innerHTML = `<div class="empty-state">${escapeHtml(error.message || 'Administration workspace unavailable.')}</div>`;
  }
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
