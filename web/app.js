/**
 * My K Library - Adaptive Web App (Desktop & Mobile)
 * Powered by Supabase Backend
 */

// 1. Supabase Initialization
const SUPABASE_URL = "https://kgvopwljwpeqkaindqbs.supabase.co";
const SUPABASE_ANON_KEY = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJpc3MiOiJzdXBhYmFzZSIsInJlZiI6Imtndm9wd2xqd3BlcWthaW5kcWJzIiwicm9sZSI6ImFub24iLCJpYXQiOjE3OTI5NjYxMjAsImV4cCI6MjEwODU0MjEyMH0.M1MN2YSrZgCWC__9YOWVXRG34zxE8Wd7GrqnY6g1HHM";

const sbClient = window.supabase.createClient(SUPABASE_URL, SUPABASE_ANON_KEY);

// State
let allBooks = [];
let allLibraries = [];
let allCategories = [];
let activeLibrary = "Default Library";
let activeCategory = "";
let searchQuery = "";
let geminiApiKey = "";
let html5QrScanner = null;
let currentBookPages = [];
let fsCameraStream = null;
let fsActiveMode = 'front';
let fsFacingMode = 'environment';
let fsIsTorchOn = false;
let touchMode = localStorage.getItem("touchMode") === "true";
if (localStorage.getItem("touchMode") === null && (navigator.maxTouchPoints > 0 || 'ontouchstart' in window)) {
  touchMode = true;
}

// DOM Elements
const loadingIndicator = document.getElementById("loadingIndicator");
const emptyState = document.getElementById("emptyState");
const desktopGridWrapper = document.getElementById("desktopGridWrapper");
const touchKioskGrid = document.getElementById("touchKioskGrid");
const bookTableBody = document.getElementById("bookTableBody");
const mobileCardFeed = document.getElementById("mobileCardFeed");
const btnTouchToggle = document.getElementById("btnTouchToggle");
const touchToggleText = document.getElementById("touchToggleText");
const btnSidebarTouch = document.getElementById("btnSidebarTouch");
const sidebarTouchText = document.getElementById("sidebarTouchText");

const librarySelect = document.getElementById("librarySelect");
const mobileLibrarySelect = document.getElementById("mobileLibrarySelect");
const currentLibraryTitle = document.getElementById("currentLibraryTitle");
const statsSubtitle = document.getElementById("statsSubtitle");
const statTotalBooks = document.getElementById("statTotalBooks");
const statCategories = document.getElementById("statCategories");

const searchInput = document.getElementById("searchInput");
const clearSearchBtn = document.getElementById("clearSearchBtn");
const categoryList = document.getElementById("categoryList");
const categoryPills = document.getElementById("categoryPills");

// Modals
const bookModal = document.getElementById("bookModal");
const bookForm = document.getElementById("bookForm");
const modalBookTitle = document.getElementById("modalBookTitle");
const scannerModal = document.getElementById("scannerModal");
const librariesModal = document.getElementById("librariesModal");

// Auth State & DOM Elements
let currentUser = null;
let pingIntervalId = null;
let isDataLoaded = false;

const loginScreen = document.getElementById("loginScreen");
const appContainer = document.getElementById("app");
const loginForm = document.getElementById("loginForm");
const loginUsername = document.getElementById("loginUsername");
const loginPassword = document.getElementById("loginPassword");
const btnTogglePassword = document.getElementById("btnTogglePassword");
const rememberMe = document.getElementById("rememberMe");
const btnLoginSubmit = document.getElementById("btnLoginSubmit");
const btnLoginText = document.getElementById("btnLoginText");
const btnLoginSpinner = document.getElementById("btnLoginSpinner");
const loginError = document.getElementById("loginError");

const btnRefreshState = document.getElementById("btnRefreshState");
const metricPing = document.getElementById("metricPing");
const metricDbHealth = document.getElementById("metricDbHealth");
const statePulseDot = document.getElementById("statePulseDot");

const btnSidebarLogout = document.getElementById("btnSidebarLogout");
const btnTopLogout = document.getElementById("btnTopLogout");
const userMeta = document.getElementById("userMeta");

// ============================================================================
// Initialization & Authentication
// ============================================================================
document.addEventListener("DOMContentLoaded", async () => {
  setupEventListeners();
  applyTouchModeUI();
  initAuth();
});

function initAuth() {
  try {
    const saved = localStorage.getItem("k_library_user_session") || sessionStorage.getItem("k_library_user_session");
    if (saved) {
      currentUser = JSON.parse(saved);
      showAppUI();
      loadInitialAppData();
      return;
    }
  } catch (e) {
    console.warn("Session restore error", e);
  }
  showLoginUI();
}

function showLoginUI() {
  if (loginScreen) loginScreen.style.display = "flex";
  if (appContainer) appContainer.style.display = "none";
  checkBackendServerState();
  if (!pingIntervalId) {
    pingIntervalId = setInterval(checkBackendServerState, 12000);
  }
}

function showAppUI() {
  if (pingIntervalId) {
    clearInterval(pingIntervalId);
    pingIntervalId = null;
  }
  if (loginScreen) loginScreen.style.display = "none";
  if (appContainer) appContainer.style.display = "flex";

  if (userMeta && currentUser) {
    const roleBadge = currentUser.role ? ` (${currentUser.role})` : "";
    userMeta.textContent = `User: ${currentUser.username}${roleBadge}`;
  }
}

async function loadInitialAppData() {
  if (isDataLoaded) return;
  isDataLoaded = true;
  await loadGeminiKey();
  await loadLibraries();
  await loadCategories();
  await loadBooks();
  setupRealtimeSubscription();
}

// Live Backend Ping & Health Check
async function checkBackendServerState() {
  if (!metricPing || !statePulseDot) return;
  
  const startTime = performance.now();
  try {
    // Ping Supabase with lightweight parallel count queries
    const [booksRes, libsRes] = await Promise.all([
      sbClient.from("books").select("id", { count: "exact" }).limit(1),
      sbClient.from("libraries").select("name", { count: "exact" }).limit(1)
    ]);

    const latency = Math.round(performance.now() - startTime);

    if (booksRes.error) throw booksRes.error;
    if (libsRes.error) throw libsRes.error;

    const bookCount = booksRes.count !== null ? booksRes.count : "—";
    const libCount = libsRes.count !== null ? libsRes.count : "—";

    metricPing.innerHTML = `<span class="badge-status online">🟢 Online</span> (${latency} ms)`;
    statePulseDot.className = "pulse-indicator online";
    if (metricDbHealth) {
      metricDbHealth.textContent = `${bookCount} books cataloged across ${libCount} libraries`;
    }
  } catch (err) {
    const latency = Math.round(performance.now() - startTime);
    console.error("Backend health check failed:", err);
    metricPing.innerHTML = `<span class="badge-status offline">🔴 Offline</span> (${latency} ms)`;
    statePulseDot.className = "pulse-indicator offline";
    if (metricDbHealth) {
      metricDbHealth.textContent = "Supabase unreachable / Network error";
    }
  }
}

// Authentication Handlers
async function handleLogin(e) {
  if (e && e.preventDefault) e.preventDefault();

  const username = (loginUsername.value || "").trim();
  const password = (loginPassword.value || "").trim();

  if (!username || !password) {
    showLoginError("Please enter both username and password.");
    return;
  }

  // UI loading state
  btnLoginSubmit.disabled = true;
  btnLoginText.textContent = "Verifying...";
  btnLoginSpinner.style.display = "inline";
  if (loginError) loginError.style.display = "none";

  try {
    let sessionUser = null;

    // 1. Check SuperAdmin Master Account
    if (username.toLowerCase() === "admin" && password === "2026") {
      sessionUser = {
        username: "admin",
        full_name: "Super Administrator",
        role: "SuperAdmin",
        loggedAt: Date.now()
      };
    } else {
      // 2. Query Supabase app_users table
      const { data, error } = await sbClient
        .from("app_users")
        .select("id, username, password, full_name, role")
        .eq("username", username)
        .maybeSingle();

      if (error) throw error;

      if (!data || data.password !== password) {
        throw new Error("Invalid username or password. Check credentials and try again.");
      }

      sessionUser = {
        username: data.username,
        full_name: data.full_name || data.username,
        role: data.role || "Member",
        loggedAt: Date.now()
      };
    }

    // Persist Session
    if (rememberMe && rememberMe.checked) {
      localStorage.setItem("k_library_user_session", JSON.stringify(sessionUser));
    } else {
      sessionStorage.setItem("k_library_user_session", JSON.stringify(sessionUser));
    }

    currentUser = sessionUser;
    showAppUI();
    loadInitialAppData();

  } catch (err) {
    console.error("Login failed:", err);
    showLoginError(err.message || "Failed to authenticate.");
  } finally {
    btnLoginSubmit.disabled = false;
    btnLoginText.textContent = "Sign In to Library";
    btnLoginSpinner.style.display = "none";
  }
}

function showLoginError(msg) {
  if (!loginError) return;
  loginError.textContent = "⚠️ " + msg;
  loginError.style.display = "flex";
}

function handleSignOut() {
  localStorage.removeItem("k_library_user_session");
  sessionStorage.removeItem("k_library_user_session");
  currentUser = null;
  if (loginPassword) loginPassword.value = "";
  if (loginError) loginError.style.display = "none";
  showLoginUI();
}

async function loadGeminiKey() {
  try {
    const { data } = await sbClient.from("app_settings").select("setting_value").eq("key_name", "GeminiApiKey").single();
    if (data && data.setting_value) {
      geminiApiKey = data.setting_value;
    }
  } catch (err) {
    console.warn("Could not retrieve Gemini API key from settings", err);
  }
}

async function loadLibraries() {
  try {
    const { data, error } = await sbClient.from("libraries").select("name").order("name");
    if (error) throw error;
    allLibraries = data.map(d => d.name);

    if (allLibraries.length === 0) {
      allLibraries = ["Default Library"];
    }

    renderLibrarySelectors();
  } catch (err) {
    console.error("Error loading libraries:", err);
  }
}

function renderLibrarySelectors() {
  librarySelect.innerHTML = "";
  mobileLibrarySelect.innerHTML = "";
  const modalLibSelect = document.getElementById("bookLibrary");
  modalLibSelect.innerHTML = "";

  allLibraries.forEach(lib => {
    const opt1 = new Option(lib, lib, lib === activeLibrary, lib === activeLibrary);
    const opt2 = new Option(lib, lib, lib === activeLibrary, lib === activeLibrary);
    const opt3 = new Option(lib, lib, lib === activeLibrary, lib === activeLibrary);
    librarySelect.add(opt1);
    mobileLibrarySelect.add(opt2);
    modalLibSelect.add(opt3);
  });

  currentLibraryTitle.textContent = activeLibrary;
}

async function loadCategories() {
  try {
    const { data, error } = await sbClient.from("book_categories").select("id, name").order("name");
    if (error) throw error;
    allCategories = data;
    renderCategories();
  } catch (err) {
    console.error("Error loading categories:", err);
  }
}

function renderCategories() {
  // Sidebar list
  categoryList.innerHTML = `<div class="category-item ${activeCategory === '' ? 'active' : ''}" data-cat="">All Genres</div>`;
  // Mobile pills
  categoryPills.innerHTML = `<button class="pill ${activeCategory === '' ? 'active' : ''}" data-cat="">All</button>`;
  // Modal category dropdown
  const bookCategorySelect = document.getElementById("bookCategory");
  bookCategorySelect.innerHTML = `<option value="">-- None / Select Genre --</option>`;

  allCategories.forEach(cat => {
    // Sidebar
    const item = document.createElement("div");
    item.className = `category-item ${activeCategory === cat.name ? 'active' : ''}`;
    item.textContent = cat.name;
    item.dataset.cat = cat.name;
    item.onclick = () => selectCategory(cat.name);
    categoryList.appendChild(item);

    // Pill
    const pill = document.createElement("button");
    pill.className = `pill ${activeCategory === cat.name ? 'active' : ''}`;
    pill.textContent = cat.name;
    pill.dataset.cat = cat.name;
    pill.onclick = () => selectCategory(cat.name);
    categoryPills.appendChild(pill);

    // Modal
    const opt = new Option(cat.name, cat.name);
    bookCategorySelect.add(opt);
  });

  statCategories.textContent = allCategories.length;
}

function selectCategory(catName) {
  activeCategory = catName;
  document.querySelectorAll(".category-item, .pill").forEach(el => {
    if (el.dataset.cat === catName) {
      el.classList.add("active");
    } else {
      el.classList.remove("active");
    }
  });
  renderFilteredCatalog();
}

async function loadBooks() {
  try {
    loadingIndicator.style.display = "block";
    emptyState.style.display = "none";
    desktopGridWrapper.style.display = "none";
    mobileCardFeed.style.display = "none";

    const { data, error } = await sbClient
      .from("books")
      .select("*")
      .order("id", { ascending: false });

    if (error) throw error;
    allBooks = data || [];
    renderFilteredCatalog();
  } catch (err) {
    console.error("Error loading books:", err);
    loadingIndicator.innerHTML = `<p style="color:var(--danger)">Error loading books: ${err.message}</p>`;
  } finally {
    loadingIndicator.style.display = "none";
  }
}

function setupRealtimeSubscription() {
  sbClient.channel("public:books")
    .on("postgres_changes", { event: "*", schema: "public", table: "books" }, async () => {
      document.getElementById("syncStatus").textContent = "Cloud Updating...";
      await loadBooks();
      document.getElementById("syncStatus").textContent = "Cloud Synced";
    })
    .subscribe();
}

// ============================================================================
// Filtering & Rendering
// ============================================================================
function getFilteredBooks() {
  return allBooks.filter(b => {
    const inLibrary = !b.library_name || b.library_name === activeLibrary;
    const inCategory = !activeCategory || b.genre === activeCategory;

    let inSearch = true;
    if (searchQuery) {
      const q = searchQuery.toLowerCase();
      const qClean = q.replace(/[-\s]/g, "");
      const title = (b.title || "").toLowerCase();
      const author = (b.author || "").toLowerCase();
      const isbn = (b.isbn || "").toLowerCase();
      const isbnClean = isbn.replace(/[-\s]/g, "");
      const desc = (b.description || "").toLowerCase();
      inSearch = title.includes(q) || author.includes(q) || isbn.includes(q) || (qClean.length >= 3 && isbnClean.includes(qClean)) || desc.includes(q);
    }

    return inLibrary && inCategory && inSearch;
  });
}

function toggleTouchMode() {
  touchMode = !touchMode;
  localStorage.setItem("touchMode", touchMode ? "true" : "false");
  applyTouchModeUI();
  renderFilteredCatalog();
}

function applyTouchModeUI() {
  document.body.classList.toggle("touch-mode", touchMode);
  if (btnTouchToggle) {
    btnTouchToggle.classList.toggle("btn-touch-active", touchMode);
    touchToggleText.textContent = touchMode ? "Touch UI: ON" : "Touch UI";
  }
  if (sidebarTouchText) {
    sidebarTouchText.textContent = touchMode ? "Touch UI: ON" : "Touch Screen UI";
  }
}

function renderFilteredCatalog() {
  const books = getFilteredBooks();
  statTotalBooks.textContent = books.length;
  statsSubtitle.textContent = `${books.length} book(s) shown in ${activeLibrary}`;

  const statEbooksCount = document.getElementById("statEbooksCount");
  if (statEbooksCount) {
    const totalEbooks = allBooks.filter(b => b.is_ebook || b.reading_url || b.library_name === "E-Books Archive").length;
    statEbooksCount.textContent = totalEbooks;
  }

  if (books.length === 0) {
    emptyState.style.display = "block";
    desktopGridWrapper.style.display = "none";
    touchKioskGrid.style.display = "none";
    mobileCardFeed.style.display = "none";
    return;
  }

  emptyState.style.display = "none";

  if (window.innerWidth >= 768) {
    if (touchMode) {
      desktopGridWrapper.style.display = "none";
      touchKioskGrid.style.display = "grid";
      renderTouchKioskGrid(books);
    } else {
      desktopGridWrapper.style.display = "block";
      touchKioskGrid.style.display = "none";
      renderDesktopGrid(books);
    }
    mobileCardFeed.style.display = "none";
  } else {
    desktopGridWrapper.style.display = "none";
    touchKioskGrid.style.display = "none";
    mobileCardFeed.style.display = "flex";
    renderMobileCards(books);
  }
}

function renderTouchKioskGrid(books) {
  touchKioskGrid.innerHTML = "";
  books.forEach(book => {
    const card = document.createElement("div");
    card.className = "touch-card";

    const coverHtml = book.cover_path && book.cover_path.startsWith("http")
      ? `<img src="${escapeHtml(book.cover_path)}" alt="Cover" class="touch-card-cover" onerror="this.outerHTML='<div class=\\'touch-card-cover-placeholder\\'>📖</div>'">`
      : `<div class="touch-card-cover-placeholder">📖</div>`;

    const isEbook = !!(book.is_ebook || book.reading_url || book.library_name === "E-Books Archive");

    card.innerHTML = `
      <div class="touch-card-cover-wrap" onclick="openEditBookModal(${book.id})">${coverHtml}</div>
      <div style="flex:1; display:flex; flex-direction:column; justify-content:space-between;">
        <div>
          <div class="touch-card-title" onclick="openEditBookModal(${book.id})">${escapeHtml(book.title)}</div>
          <div class="touch-card-author">${escapeHtml(book.author || "Unknown Author")}</div>
          <div class="touch-card-meta">
            ${isEbook ? `<span class="ebook-badge">📱 ${escapeHtml(book.ebook_format || "E-Book")}</span>` : ""}
            ${book.genre ? `<span class="category-tag">${escapeHtml(book.genre)}</span>` : ""}
            ${book.pub_year ? `<span style="font-size:0.82rem; color:var(--text-muted); font-weight:600;">${book.pub_year}</span>` : ""}
            ${book.isbn ? `<span class="isbn-pill">ISBN: ${escapeHtml(book.isbn)}</span>` : ""}
          </div>
        </div>
        <div class="touch-card-actions">
          ${book.reading_url ? `<a href="${escapeHtml(book.reading_url)}" target="_blank" rel="noopener noreferrer" class="btn btn-read-link" title="Open Free Reader">📖 Read Online</a>` : ""}
          <button class="btn btn-secondary btn-sm" onclick="openEditBookModal(${book.id})">✎ Edit</button>
          <button class="btn btn-secondary btn-sm" style="color:var(--danger);" onclick="deleteBook(${book.id}, '${escapeQuote(book.title)}')">✖</button>
        </div>
      </div>
    `;
    touchKioskGrid.appendChild(card);
  });
}

function renderDesktopGrid(books) {
  bookTableBody.innerHTML = "";
  books.forEach(book => {
    const tr = document.createElement("tr");

    const coverHtml = book.cover_path && book.cover_path.startsWith("http")
      ? `<img src="${escapeHtml(book.cover_path)}" alt="Cover" class="table-cover" onerror="this.outerHTML='<div class=\\'table-cover-placeholder\\'>📖</div>'">`
      : `<div class="table-cover-placeholder">📖</div>`;

    const isEbook = !!(book.is_ebook || book.reading_url || book.library_name === "E-Books Archive");

    tr.innerHTML = `
      <td>${coverHtml}</td>
      <td class="book-title-cell">
        ${escapeHtml(book.title)}
        ${isEbook ? `<span class="ebook-badge" style="margin-left:6px;">📱 ${escapeHtml(book.ebook_format || "E-Book")}</span>` : ""}
      </td>
      <td>${escapeHtml(book.author || "—")}</td>
      <td>${book.genre ? `<span class="category-tag">${escapeHtml(book.genre)}</span>` : "—"}</td>
      <td>${book.pub_year || "—"}</td>
      <td>${book.isbn ? `<span class="isbn-pill">${escapeHtml(book.isbn)}</span>` : "—"}</td>
      <td style="text-align: right; white-space: nowrap;">
        ${book.reading_url ? `<a href="${escapeHtml(book.reading_url)}" target="_blank" rel="noopener noreferrer" class="btn btn-read-link" style="margin-right:6px;" title="Open Free Online Reader">📖 Read Online</a>` : ""}
        <button class="btn btn-secondary btn-sm" onclick="openEditBookModal(${book.id})">✎ Edit</button>
        <button class="btn btn-secondary btn-sm" style="color:var(--danger);" onclick="deleteBook(${book.id}, '${escapeQuote(book.title)}')">✖</button>
      </td>
    `;
    bookTableBody.appendChild(tr);
  });
}

function renderMobileCards(books) {
  mobileCardFeed.innerHTML = "";
  books.forEach(book => {
    const card = document.createElement("div");
    card.className = "mobile-book-card";

    const coverHtml = book.cover_path && book.cover_path.startsWith("http")
      ? `<img src="${escapeHtml(book.cover_path)}" alt="Cover" class="card-cover" onerror="this.outerHTML='<div class=\\'card-cover-placeholder\\'>📖</div>'">`
      : `<div class="card-cover-placeholder">📖</div>`;

    const isEbook = !!(book.is_ebook || book.reading_url || book.library_name === "E-Books Archive");

    card.innerHTML = `
      <div class="card-cover-wrap">${coverHtml}</div>
      <div class="card-body">
        <div>
          <div class="card-title">
            ${escapeHtml(book.title)}
            ${isEbook ? `<span class="ebook-badge" style="margin-left:4px; font-size:0.7rem;">📱 ${escapeHtml(book.ebook_format || "E-Book")}</span>` : ""}
          </div>
          <div class="card-author">${escapeHtml(book.author || "Unknown Author")}</div>
          <div class="card-meta-row">
            ${book.genre ? `<span class="category-tag">${escapeHtml(book.genre)}</span>` : ""}
            ${book.pub_year ? `<span style="font-size:0.75rem; color:var(--text-muted);">${book.pub_year}</span>` : ""}
            ${book.isbn ? `<span class="isbn-pill">ISBN: ${escapeHtml(book.isbn)}</span>` : ""}
          </div>
        </div>
        <div class="card-actions-row">
          ${book.reading_url ? `<a href="${escapeHtml(book.reading_url)}" target="_blank" rel="noopener noreferrer" class="btn btn-read-link" style="flex:1;">📖 Read Free</a>` : ""}
          <button class="btn btn-secondary btn-sm" onclick="openEditBookModal(${book.id})">✎ Edit</button>
          <button class="btn btn-secondary btn-sm" style="color:var(--danger);" onclick="deleteBook(${book.id}, '${escapeQuote(book.title)}')">Delete</button>
        </div>
      </div>
    `;
    mobileCardFeed.appendChild(card);
  });
}

// ============================================================================
// Event Listeners
// ============================================================================
function setupEventListeners() {
  // Library switch
  librarySelect.addEventListener("change", (e) => {
    activeLibrary = e.target.value;
    mobileLibrarySelect.value = activeLibrary;
    currentLibraryTitle.textContent = activeLibrary;
    renderFilteredCatalog();
  });

  mobileLibrarySelect.addEventListener("change", (e) => {
    activeLibrary = e.target.value;
    librarySelect.value = activeLibrary;
    currentLibraryTitle.textContent = activeLibrary;
    renderFilteredCatalog();
  });

  // Search input
  searchInput.addEventListener("input", (e) => {
    searchQuery = e.target.value.trim();
    clearSearchBtn.style.display = searchQuery ? "block" : "none";
    renderFilteredCatalog();
  });

  clearSearchBtn.addEventListener("click", () => {
    searchInput.value = "";
    searchQuery = "";
    clearSearchBtn.style.display = "none";
    renderFilteredCatalog();
  });

  // Mobile drawer toggle
  const mobileMenuBtn = document.getElementById("mobileMenuBtn");
  const sidebar = document.getElementById("sidebar");
  mobileMenuBtn.addEventListener("click", () => {
    sidebar.classList.toggle("open");
  });

  // Close sidebar on click outside in mobile
  document.addEventListener("click", (e) => {
    if (sidebar.classList.contains("open") && !sidebar.contains(e.target) && e.target !== mobileMenuBtn) {
      sidebar.classList.remove("open");
    }
  });

  // Touch Mode toggles
  if (btnTouchToggle) btnTouchToggle.onclick = toggleTouchMode;
  if (btnSidebarTouch) {
    btnSidebarTouch.onclick = (e) => {
      e.preventDefault();
      toggleTouchMode();
    };
  }
  window.addEventListener("resize", renderFilteredCatalog);

  // Modal open triggers
  document.getElementById("btnTopAdd").onclick = openAddBookModal;
  document.getElementById("btnSidebarAdd").onclick = openAddBookModal;
  document.getElementById("btnEmptyAdd").onclick = openAddBookModal;
  document.getElementById("navMobileAdd").onclick = openAddBookModal;

  document.getElementById("btnTopScan").onclick = openScannerModal;
  document.getElementById("btnSidebarScan").onclick = openScannerModal;
  document.getElementById("navMobileScan").onclick = openScannerModal;

  document.getElementById("btnSidebarLibraries").onclick = openLibrariesModal;
  document.getElementById("navMobileLibs").onclick = openLibrariesModal;

  // Modal close triggers
  document.getElementById("modalCloseBtn").onclick = closeBookModal;
  document.getElementById("modalCancelBtn").onclick = closeBookModal;
  document.getElementById("scannerCloseBtn").onclick = closeScannerModal;
  document.getElementById("librariesCloseBtn").onclick = closeLibrariesModal;

  // Form tab switching inside Book Modal
  document.querySelectorAll(".form-tabs .tab-btn").forEach(btn => {
    btn.addEventListener("click", () => {
      document.querySelectorAll(".form-tabs .tab-btn").forEach(b => b.classList.remove("active"));
      document.querySelectorAll(".tab-panel").forEach(p => p.classList.remove("active"));
      btn.classList.add("active");
      document.getElementById(btn.dataset.tab).classList.add("active");
    });
  });

  // Live cover image preview
  const bookCoverUrl = document.getElementById("bookCoverUrl");
  const coverPreviewImg = document.getElementById("coverPreviewImg");
  bookCoverUrl.addEventListener("input", (e) => {
    const url = e.target.value.trim();
    if (url && (url.startsWith("http://") || url.startsWith("https://"))) {
      coverPreviewImg.src = url;
      coverPreviewImg.style.display = "block";
    } else {
      coverPreviewImg.style.display = "none";
    }
  });

  // Save Book Form Submission
  bookForm.addEventListener("submit", handleSaveBook);

  // Scanner Modal Buttons
  document.getElementById("btnTriggerGoogleLookup").onclick = () => {
    const isbn = document.getElementById("scanInputISBN").value.trim();
    if (!isbn) return alert("Please enter an ISBN first.");
    performGoogleLookup(isbn);
  };

  document.getElementById("btnTriggerAiLookup").onclick = () => {
    const isbn = document.getElementById("scanInputISBN").value.trim();
    if (!isbn) return alert("Please enter an ISBN first.");
    performGeminiLookup(isbn);
  };


  // Scanner Modal Buttons
  const btnScanCoverAi = document.getElementById("btnScanCoverAi");
  if (btnScanCoverAi) btnScanCoverAi.onclick = () => openFullscreenCamera("front");

  const btnScanBackAi = document.getElementById("btnScanBackAi");
  if (btnScanBackAi) btnScanBackAi.onclick = () => openFullscreenCamera("back");

  const btnTriggerCoverScan = document.getElementById("btnTriggerCoverScan");
  if (btnTriggerCoverScan) btnTriggerCoverScan.onclick = () => openFullscreenCamera("front");

  // Covers & Scanned Pages UI Listeners
  const btnScanFrontCoverModal = document.getElementById("btnScanFrontCoverModal");
  if (btnScanFrontCoverModal) btnScanFrontCoverModal.onclick = () => openFullscreenCamera("front");

  const btnScanBackCoverModal = document.getElementById("btnScanBackCoverModal");
  if (btnScanBackCoverModal) btnScanBackCoverModal.onclick = () => openFullscreenCamera("back");

  const btnScanNewPageModal = document.getElementById("btnScanNewPageModal");
  if (btnScanNewPageModal) btnScanNewPageModal.onclick = () => openFullscreenCamera("page");

  const btnClearFrontCover = document.getElementById("btnClearFrontCover");
  if (btnClearFrontCover) {
    btnClearFrontCover.onclick = () => {
      document.getElementById("bookCoverUrl").value = "";
      const img = document.getElementById("coverPreviewImg");
      const ph = document.getElementById("coverPlaceholderBox");
      if (img) img.style.display = "none";
      if (ph) ph.style.display = "flex";
    };
  }

  const btnClearBackCover = document.getElementById("btnClearBackCover");
  if (btnClearBackCover) {
    btnClearBackCover.onclick = () => {
      document.getElementById("bookBackUrl").value = "";
      const img = document.getElementById("backPreviewImg");
      const ph = document.getElementById("backPlaceholderBox");
      if (img) img.style.display = "none";
      if (ph) ph.style.display = "flex";
    };
  }

  const btnUploadFrontCoverModal = document.getElementById("btnUploadFrontCoverModal");
  const frontCoverFileInput = document.getElementById("frontCoverFileInput");
  if (btnUploadFrontCoverModal && frontCoverFileInput) {
    btnUploadFrontCoverModal.onclick = () => frontCoverFileInput.click();
    frontCoverFileInput.onchange = (e) => {
      const file = e.target.files && e.target.files[0];
      if (!file) return;
      const reader = new FileReader();
      reader.onload = (ev) => {
        const dataUrl = ev.target.result;
        document.getElementById("bookCoverUrl").value = dataUrl;
        const img = document.getElementById("coverPreviewImg");
        const ph = document.getElementById("coverPlaceholderBox");
        if (img) { img.src = dataUrl; img.style.display = "block"; }
        if (ph) ph.style.display = "none";
        performCoverAiScan(dataUrl.split(",")[1]);
      };
      reader.readAsDataURL(file);
    };
  }

  const btnUploadBackCoverModal = document.getElementById("btnUploadBackCoverModal");
  const backCoverFileInput = document.getElementById("backCoverFileInput");
  if (btnUploadBackCoverModal && backCoverFileInput) {
    btnUploadBackCoverModal.onclick = () => backCoverFileInput.click();
    backCoverFileInput.onchange = (e) => {
      const file = e.target.files && e.target.files[0];
      if (!file) return;
      const reader = new FileReader();
      reader.onload = (ev) => {
        const dataUrl = ev.target.result;
        document.getElementById("bookBackUrl").value = dataUrl;
        const img = document.getElementById("backPreviewImg");
        const ph = document.getElementById("backPlaceholderBox");
        if (img) { img.src = dataUrl; img.style.display = "block"; }
        if (ph) ph.style.display = "none";
        performBackCoverAiScan(dataUrl.split(",")[1]);
      };
      reader.readAsDataURL(file);
    };
  }

  const btnUploadNewPagesModal = document.getElementById("btnUploadNewPagesModal");
  const pagesFileInput = document.getElementById("pagesFileInput");
  if (btnUploadNewPagesModal && pagesFileInput) {
    btnUploadNewPagesModal.onclick = () => pagesFileInput.click();
    pagesFileInput.onchange = (e) => {
      const files = Array.from(e.target.files || []);
      files.forEach((file, idx) => {
        const reader = new FileReader();
        reader.onload = (ev) => {
          currentBookPages.push({
            id: Date.now() + idx,
            title: file.name ? file.name.replace(/\.[^/.]+$/, "") : "Page " + (currentBookPages.length + 1),
            url: ev.target.result
          });
          renderScannedPagesGallery();
        };
        reader.readAsDataURL(file);
      });
    };
  }

  // Fullscreen Camera Controls
  const btnCloseFullscreenCam = document.getElementById("btnCloseFullscreenCam");
  if (btnCloseFullscreenCam) btnCloseFullscreenCam.onclick = closeFullscreenCamera;

  const btnModeFront = document.getElementById("btnModeFront");
  if (btnModeFront) btnModeFront.onclick = () => { fsActiveMode = "front"; updateCameraModeUI(); };

  const btnModeBack = document.getElementById("btnModeBack");
  if (btnModeBack) btnModeBack.onclick = () => { fsActiveMode = "back"; updateCameraModeUI(); };

  const btnModePage = document.getElementById("btnModePage");
  if (btnModePage) btnModePage.onclick = () => { fsActiveMode = "page"; updateCameraModeUI(); };

  const btnToggleTorch = document.getElementById("btnToggleTorch");
  if (btnToggleTorch) btnToggleTorch.onclick = toggleTorch;

  const btnFlipCamera = document.getElementById("btnFlipCamera");
  if (btnFlipCamera) btnFlipCamera.onclick = flipCamera;

  const btnCameraShutter = document.getElementById("btnCameraShutter");
  if (btnCameraShutter) btnCameraShutter.onclick = handleShutterCapture;

  const btnCameraUploadFile = document.getElementById("btnCameraUploadFile");
  const fsCameraFileInput = document.getElementById("fsCameraFileInput");
  if (btnCameraUploadFile && fsCameraFileInput) {
    btnCameraUploadFile.onclick = () => fsCameraFileInput.click();
    fsCameraFileInput.onchange = (e) => {
      const file = e.target.files && e.target.files[0];
      if (!file) return;
      const reader = new FileReader();
      reader.onload = (ev) => {
        const dataUrl = ev.target.result;
        const base64 = dataUrl.split(",")[1];
        if (fsActiveMode === "front") {
          document.getElementById("bookCoverUrl").value = dataUrl;
          const img = document.getElementById("coverPreviewImg");
          const ph = document.getElementById("coverPlaceholderBox");
          if (img) { img.src = dataUrl; img.style.display = "block"; }
          if (ph) ph.style.display = "none";
          closeFullscreenCamera();
          closeScannerModal();
          openAddBookModal();
          document.querySelectorAll(".form-tabs .tab-btn").forEach(b => b.classList.remove("active"));
          document.querySelectorAll(".tab-panel").forEach(p => p.classList.remove("active"));
          const tabMediaBtn = document.querySelector(".form-tabs .tab-btn[data-tab='tabMedia']");
          if (tabMediaBtn) tabMediaBtn.classList.add("active");
          const tabMediaPanel = document.getElementById("tabMedia");
          if (tabMediaPanel) tabMediaPanel.classList.add("active");
          performCoverAiScan(base64);
        } else if (fsActiveMode === "back") {
          document.getElementById("bookBackUrl").value = dataUrl;
          const img = document.getElementById("backPreviewImg");
          const ph = document.getElementById("backPlaceholderBox");
          if (img) { img.src = dataUrl; img.style.display = "block"; }
          if (ph) ph.style.display = "none";
          closeFullscreenCamera();
          closeScannerModal();
          openAddBookModal();
          document.querySelectorAll(".form-tabs .tab-btn").forEach(b => b.classList.remove("active"));
          document.querySelectorAll(".tab-panel").forEach(p => p.classList.remove("active"));
          const tabMediaBtn = document.querySelector(".form-tabs .tab-btn[data-tab='tabMedia']");
          if (tabMediaBtn) tabMediaBtn.classList.add("active");
          const tabMediaPanel = document.getElementById("tabMedia");
          if (tabMediaPanel) tabMediaPanel.classList.add("active");
          performBackCoverAiScan(base64);
        } else {
          currentBookPages.push({
            id: Date.now(),
            title: file.name ? file.name.replace(/\.[^/.]+$/, "") : "Page " + (currentBookPages.length + 1),
            url: dataUrl
          });
          renderScannedPagesGallery();
        }
      };
      reader.readAsDataURL(file);
    };
  }

  const btnViewScannedGallery = document.getElementById("btnViewScannedGallery");
  if (btnViewScannedGallery) {
    btnViewScannedGallery.onclick = () => {
      closeFullscreenCamera();
      closeScannerModal();
      openAddBookModal();
      document.querySelectorAll(".form-tabs .tab-btn").forEach(b => b.classList.remove("active"));
      document.querySelectorAll(".tab-panel").forEach(p => p.classList.remove("active"));
      const tabMediaBtn = document.querySelector(".form-tabs .tab-btn[data-tab='tabMedia']");
      if (tabMediaBtn) tabMediaBtn.classList.add("active");
      const tabMediaPanel = document.getElementById("tabMedia");
      if (tabMediaPanel) tabMediaPanel.classList.add("active");
    };
  }

  // Lightbox
  const btnCloseLightbox = document.getElementById("btnCloseLightbox");
  if (btnCloseLightbox) btnCloseLightbox.onclick = closeLightbox;
  const pageLightboxModal = document.getElementById("pageLightboxModal");
  if (pageLightboxModal) {
    pageLightboxModal.onclick = (e) => {
      if (e.target === pageLightboxModal) closeLightbox();
    };
  }


  const btnUploadCoverAi = document.getElementById("btnUploadCoverAi");
  const coverFileInput = document.getElementById("coverFileInput");
  if (btnUploadCoverAi && coverFileInput) {
    btnUploadCoverAi.onclick = () => coverFileInput.click();
    coverFileInput.onchange = (e) => {
      const file = e.target.files && e.target.files[0];
      if (!file) return;
      const reader = new FileReader();
      reader.onload = (loadEvt) => {
        const dataUrl = loadEvt.target.result;
        const base64 = dataUrl.split(",")[1];
        performCoverAiScan(base64);
      };
      reader.readAsDataURL(file);
    };
  }

  // Create Library
  document.getElementById("btnCreateLib").onclick = handleCreateLibrary;

  // E-Books Bookshelf Quick Navigation
  const btnSidebarEbooks = document.getElementById("btnSidebarEbooks");
  const statEbooksCard = document.getElementById("statEbooksCard");
  if (statEbooksCard) {
    statEbooksCard.addEventListener("click", () => {
      activeLibrary = "E-Books Archive";
      if (librarySelect) librarySelect.value = activeLibrary;
      if (mobileLibrarySelect) mobileLibrarySelect.value = activeLibrary;
      currentLibraryTitle.textContent = activeLibrary;
      document.querySelectorAll(".sidebar-nav .nav-item").forEach(i => i.classList.remove("active"));
      if (btnSidebarEbooks) btnSidebarEbooks.classList.add("active");
      renderFilteredCatalog();
    });
  }

  if (btnSidebarEbooks) {
    btnSidebarEbooks.addEventListener("click", (e) => {
      e.preventDefault();
      activeLibrary = "E-Books Archive";
      if (librarySelect) librarySelect.value = activeLibrary;
      if (mobileLibrarySelect) mobileLibrarySelect.value = activeLibrary;
      currentLibraryTitle.textContent = activeLibrary;
      document.querySelectorAll(".sidebar-nav .nav-item").forEach(i => i.classList.remove("active"));
      btnSidebarEbooks.classList.add("active");
      renderFilteredCatalog();
    });
  }

  // Live Test Reading Link button in Book Modal
  const bookReadingUrl = document.getElementById("bookReadingUrl");
  const btnTestReadingUrl = document.getElementById("btnTestReadingUrl");
  if (bookReadingUrl && btnTestReadingUrl) {
    bookReadingUrl.addEventListener("input", () => {
      const u = bookReadingUrl.value.trim();
      if (u && (u.startsWith("http://") || u.startsWith("https://"))) {
        btnTestReadingUrl.href = u;
        btnTestReadingUrl.style.display = "inline-flex";
      } else {
        btnTestReadingUrl.style.display = "none";
      }
    });
  }

  // Auth & Session Listeners
  if (loginForm) {
    loginForm.addEventListener("submit", handleLogin);
  }

  if (btnTogglePassword && loginPassword) {
    btnTogglePassword.addEventListener("click", () => {
      const isPw = loginPassword.type === "password";
      loginPassword.type = isPw ? "text" : "password";
      btnTogglePassword.textContent = isPw ? "🙈" : "👁️";
    });
  }



  // Backend State Refresh
  if (btnRefreshState) {
    btnRefreshState.addEventListener("click", () => {
      btnRefreshState.textContent = "⏳ Ping...";
      checkBackendServerState().finally(() => {
        btnRefreshState.textContent = "🔄 Refresh";
      });
    });
  }

  // Logout Handlers
  if (btnSidebarLogout) btnSidebarLogout.addEventListener("click", handleSignOut);
  if (btnTopLogout) btnTopLogout.addEventListener("click", handleSignOut);
}

// ============================================================================
// Book Modal Actions (Add / Edit / Save / Delete)
// ============================================================================
function openAddBookModal() {
  modalBookTitle.textContent = "➕ Add New Book";
  bookForm.reset();
  document.getElementById("editBookId").value = "";
  document.getElementById("bookLibrary").value = activeLibrary;
  document.getElementById("coverPreviewImg").style.display = "none";
  const coverPlaceholderBox = document.getElementById("coverPlaceholderBox");
  if (coverPlaceholderBox) coverPlaceholderBox.style.display = "flex";
  const backPreviewImg = document.getElementById("backPreviewImg");
  if (backPreviewImg) backPreviewImg.style.display = "none";
  const backPlaceholderBox = document.getElementById("backPlaceholderBox");
  if (backPlaceholderBox) backPlaceholderBox.style.display = "flex";
  const bookBackUrl = document.getElementById("bookBackUrl");
  if (bookBackUrl) bookBackUrl.value = "";
  currentBookPages = [];
  renderScannedPagesGallery();
  const chkEbook = document.getElementById("bookIsEbook");
  if (chkEbook) chkEbook.checked = (activeLibrary === "E-Books Archive");
  const txtReading = document.getElementById("bookReadingUrl");
  if (txtReading) txtReading.value = "";
  const btnTest = document.getElementById("btnTestReadingUrl");
  if (btnTest) btnTest.style.display = "none";
  resetModalTabs();
  bookModal.style.display = "flex";
}

window.openEditBookModal = function(id) {
  const book = allBooks.find(b => b.id === id);
  if (!book) return;

  modalBookTitle.textContent = "✎ Edit Book Details";
  document.getElementById("editBookId").value = book.id;
  document.getElementById("bookTitle").value = book.title || "";
  document.getElementById("bookAuthor").value = book.author || "";
  document.getElementById("bookCategory").value = book.genre || "";
  document.getElementById("bookISBN").value = book.isbn || "";
  document.getElementById("bookLibrary").value = book.library_name || activeLibrary;

  document.getElementById("bookPublisher").value = book.publisher || "";
  document.getElementById("bookPubYear").value = book.pub_year || "";
  document.getElementById("bookPages").value = book.pages || "";
  document.getElementById("bookLanguage").value = book.language || "";
  document.getElementById("bookSeries").value = book.series || "";
  document.getElementById("bookSubjects").value = book.subjects || "";

  document.getElementById("bookCoverUrl").value = book.cover_path || "";
  document.getElementById("bookBackUrl").value = book.back_path || "";
  document.getElementById("bookDescription").value = book.description || "";

  const coverPreviewImg = document.getElementById("coverPreviewImg");
  const coverPlaceholderBox = document.getElementById("coverPlaceholderBox");
  if (book.cover_path && (book.cover_path.startsWith("http") || book.cover_path.startsWith("data:"))) {
    if (coverPreviewImg) { coverPreviewImg.src = book.cover_path; coverPreviewImg.style.display = "block"; }
    if (coverPlaceholderBox) coverPlaceholderBox.style.display = "none";
  } else {
    if (coverPreviewImg) coverPreviewImg.style.display = "none";
    if (coverPlaceholderBox) coverPlaceholderBox.style.display = "flex";
  }

  const backPreviewImg = document.getElementById("backPreviewImg");
  const backPlaceholderBox = document.getElementById("backPlaceholderBox");
  if (book.back_path && (book.back_path.startsWith("http") || book.back_path.startsWith("data:"))) {
    if (backPreviewImg) { backPreviewImg.src = book.back_path; backPreviewImg.style.display = "block"; }
    if (backPlaceholderBox) backPlaceholderBox.style.display = "none";
  } else {
    if (backPreviewImg) backPreviewImg.style.display = "none";
    if (backPlaceholderBox) backPlaceholderBox.style.display = "flex";
  }

  // Load multi-page scans
  currentBookPages = [];
  if (book.scanned_pages) {
    try {
      currentBookPages = typeof book.scanned_pages === "string" ? JSON.parse(book.scanned_pages) : book.scanned_pages;
      if (!Array.isArray(currentBookPages)) currentBookPages = [];
    } catch (e) {
      currentBookPages = [];
    }
  }
  renderScannedPagesGallery();

  const isEb = !!(book.is_ebook || book.reading_url || book.library_name === "E-Books Archive");
  const chkEbook = document.getElementById("bookIsEbook");
  if (chkEbook) chkEbook.checked = isEb;
  const txtReading = document.getElementById("bookReadingUrl");
  if (txtReading) txtReading.value = book.reading_url || "";
  const selFormat = document.getElementById("bookEbookFormat");
  if (selFormat && book.ebook_format) selFormat.value = book.ebook_format;
  const btnTest = document.getElementById("btnTestReadingUrl");
  if (btnTest) {
    if (book.reading_url) {
      btnTest.href = book.reading_url;
      btnTest.style.display = "inline-flex";
    } else {
      btnTest.style.display = "none";
    }
  }

  resetModalTabs();
  bookModal.style.display = "flex";
};

function resetModalTabs() {
  document.querySelectorAll(".form-tabs .tab-btn").forEach(b => b.classList.remove("active"));
  document.querySelectorAll(".tab-panel").forEach(p => p.classList.remove("active"));
  document.querySelector(".form-tabs .tab-btn[data-tab='tabBasic']").classList.add("active");
  document.getElementById("tabBasic").classList.add("active");
}

function closeBookModal() {
  bookModal.style.display = "none";
}

async function handleSaveBook(e) {
  e.preventDefault();
  const editId = document.getElementById("editBookId").value;
  const isEdit = !!editId;

  const payload = {
    title: document.getElementById("bookTitle").value.trim(),
    author: document.getElementById("bookAuthor").value.trim(),
    genre: document.getElementById("bookCategory").value || null,
    isbn: document.getElementById("bookISBN").value.trim() || null,
    library_name: document.getElementById("bookLibrary").value || activeLibrary,
    publisher: document.getElementById("bookPublisher").value.trim() || null,
    pub_year: parseInt(document.getElementById("bookPubYear").value) || null,
    pages: parseInt(document.getElementById("bookPages").value) || null,
    language: document.getElementById("bookLanguage").value.trim() || null,
    series: document.getElementById("bookSeries").value.trim() || null,
    subjects: document.getElementById("bookSubjects").value.trim() || null,
    cover_path: document.getElementById("bookCoverUrl").value.trim() || null,
    back_path: document.getElementById("bookBackUrl") ? (document.getElementById("bookBackUrl").value.trim() || null) : null,
    scanned_pages: currentBookPages.length > 0 ? JSON.stringify(currentBookPages) : null,
    description: document.getElementById("bookDescription").value.trim() || null,
    is_ebook: document.getElementById("bookIsEbook") ? document.getElementById("bookIsEbook").checked : false,
    reading_url: document.getElementById("bookReadingUrl") ? (document.getElementById("bookReadingUrl").value.trim() || null) : null,
    ebook_format: document.getElementById("bookEbookFormat") ? (document.getElementById("bookEbookFormat").value || "Online / Web Reader") : "Online / Web Reader",
    updated_at: new Date().toISOString()
  };

  const saveBtn = document.getElementById("modalSaveBtn");
  saveBtn.disabled = true;
  saveBtn.textContent = "Saving to Supabase...";

  try {
    if (isEdit) {
      const { error } = await sbClient.from("books").update(payload).eq("id", editId);
      if (error) throw error;
    } else {
      payload.created_at = new Date().toISOString();
      const { error } = await sbClient.from("books").insert([payload]);
      if (error) throw error;
    }

    closeBookModal();
    await loadBooks();
  } catch (err) {
    alert("Error saving book: " + err.message);
  } finally {
    saveBtn.disabled = false;
    saveBtn.textContent = "💾 Save to Cloud";
  }
}

window.deleteBook = async function(id, title) {
  if (!confirm(`Are you sure you want to delete "${title}"?`)) return;

  try {
    const { error } = await sbClient.from("books").delete().eq("id", id);
    if (error) throw error;
    await loadBooks();
  } catch (err) {
    alert("Error deleting book: " + err.message);
  }
};

// ============================================================================
// Camera Scanner & Auto-Fill
// ============================================================================
function openScannerModal() {
  document.getElementById("scanInputISBN").value = "";
  document.getElementById("lookupFeedback").textContent = "";
  scannerModal.style.display = "flex";
  startCameraScanner();
}

function closeScannerModal() {
  stopCameraScanner();
  scannerModal.style.display = "none";
}

function startCameraScanner() {
  const statusEl = document.getElementById("scannerCameraStatus");
  statusEl.textContent = "Requesting camera stream...";

  if (!window.Html5Qrcode) {
    statusEl.textContent = "Barcode library loading...";
    return;
  }

  html5QrScanner = new Html5Qrcode("interactiveScanner");
  const config = { fps: 10, qrbox: { width: 250, height: 160 } };

  html5QrScanner.start(
    { facingMode: "environment" },
    config,
    (decodedText) => {
      // Audio feedback
      beep();
      statusEl.textContent = `Scanned ISBN: ${decodedText}!`;
      document.getElementById("scanInputISBN").value = decodedText;
      stopCameraScanner();
      performGoogleLookup(decodedText);
    },
    (errorMessage) => {
      // Periodic scanning attempt callback
    }
  ).then(() => {
    statusEl.textContent = "Camera active: Align barcode inside frame";
  }).catch((err) => {
    statusEl.textContent = "Camera not available or permission denied";
    console.warn("Camera init failed:", err);
  });
}

function stopCameraScanner() {
  if (html5QrScanner) {
    html5QrScanner.stop().then(() => {
      html5QrScanner.clear();
      html5QrScanner = null;
    }).catch(() => {
      html5QrScanner = null;
    });
  }
}

function beep() {
  try {
    const ctx = new (window.AudioContext || window.webkitAudioContext)();
    const osc = ctx.createOscillator();
    osc.frequency.setValueAtTime(800, ctx.currentTime);
    osc.connect(ctx.destination);
    osc.start();
    osc.stop(ctx.currentTime + 0.1);
  } catch(e) {}
}

async function performGoogleLookup(isbn) {
  const feedback = document.getElementById("lookupFeedback");
  feedback.textContent = `Searching Google Books API for ISBN: ${isbn}...`;
  feedback.style.color = "var(--primary)";

  try {
    const res = await fetch(`https://www.googleapis.com/books/v1/volumes?q=isbn:${encodeURIComponent(isbn)}`);
    const data = await res.json();

    if (data.totalItems > 0 && data.items && data.items[0]) {
      const info = data.items[0].volumeInfo;
      feedback.textContent = `Found: "${info.title}"! Auto-filling form...`;
      feedback.style.color = "var(--accent-green)";

      setTimeout(() => {
        closeScannerModal();
        openAddBookModal();

        document.getElementById("bookTitle").value = info.title || "";
        document.getElementById("bookAuthor").value = (info.authors && info.authors.join(", ")) || "";
        document.getElementById("bookPublisher").value = info.publisher || "";
        document.getElementById("bookPubYear").value = (info.publishedDate || "").substring(0, 4) || "";
        document.getElementById("bookPages").value = info.pageCount || "";
        document.getElementById("bookLanguage").value = info.language || "";
        document.getElementById("bookISBN").value = isbn;
        document.getElementById("bookSubjects").value = (info.categories && info.categories.join(", ")) || "";
        document.getElementById("bookDescription").value = info.description || "";

        if (info.imageLinks && (info.imageLinks.thumbnail || info.imageLinks.smallThumbnail)) {
          const coverUrl = (info.imageLinks.thumbnail || info.imageLinks.smallThumbnail).replace("http://", "https://");
          document.getElementById("bookCoverUrl").value = coverUrl;
          const preview = document.getElementById("coverPreviewImg");
          preview.src = coverUrl;
          preview.style.display = "block";
        }

        const access = (data.items[0] && data.items[0].accessInfo) || {};
        const readLink = access.webReaderLink || info.previewLink || "";
        if (readLink) {
          const txtUrl = document.getElementById("bookReadingUrl");
          if (txtUrl) txtUrl.value = readLink;
          const chkEb = document.getElementById("bookIsEbook");
          if (chkEb) chkEb.checked = true;
          const btnTest = document.getElementById("btnTestReadingUrl");
          if (btnTest) {
            btnTest.href = readLink;
            btnTest.style.display = "inline-flex";
          }
        }
      }, 700);
    } else {
      feedback.textContent = `No book found for ISBN ${isbn} on Google Books.`;
      feedback.style.color = "var(--danger)";
    }
  } catch (err) {
    feedback.textContent = `Lookup error: ${err.message}`;
    feedback.style.color = "var(--danger)";
  }
}

async function performGeminiLookup(isbn) {
  const feedback = document.getElementById("lookupFeedback");
  const warningBanner = document.getElementById("aiWarningBanner");
  const warningText = document.getElementById("aiWarningText");
  if (warningBanner) warningBanner.style.display = "none";

  if (!geminiApiKey) {
    const key = prompt("Enter your Google Gemini API Key (saved securely to database):");
    if (!key) return;
    geminiApiKey = key.trim();
    await sbClient.from("app_settings").upsert({ key_name: "GeminiApiKey", setting_value: geminiApiKey });
  }

  feedback.textContent = "Analyzing ISBN with Gemini AI (Google Search Grounding)...";
  feedback.style.color = "var(--accent-purple)";

  const candidateModels = [
    "gemini-3.5-flash-lite",
    "gemini-3.5-flash",
    "gemini-3.1-flash-lite",
    "gemini-3.8-flash"
  ];

  const promptText = `You are a strict bibliographic cataloging assistant. The user will provide an ISBN (10 or 13 digits) OR a commercial/market barcode (such as an Egyptian EAN-13 barcode starting with 622, or other publication/retail barcode): "${isbn}".

CRITICAL RULES:
1. DO NOT GUESS, FABRICATE, OR HALLUCINATE ANY BOOK DATA.
2. Do not return famous fallback books just because the barcode prefix matches a country (such as 977 or 622 for Egypt).
3. If it starts with 622, it is an Egyptian product/market barcode frequently used for Egyptian educational curricula, school booklets (مثل سلاح التلميذ أو الأضواء أو المعاصر), university materials, or commercial publications. If it starts with 978/979/977, it is an ISBN. Convert between ISBN-10 and ISBN-13 if necessary.
4. If you have access to Google Search, search for the exact barcode string to find the real book, booklet, syllabus, or author.
5. Prefer original script (e.g., Arabic script for Arabic books) over ALA-LC romanization/transliteration whenever possible.
6. If you are not 100% certain of the exact item matching this specific code, you MUST return \`null\` for the title and author fields, and set \`"found": false\`.

Return ONLY a valid JSON object in this exact structure:
{
  "found": boolean,
  "title": string or null,
  "author": string or null,
  "publisher": string or null,
  "publicationYear": string or null,
  "language": string or null,
  "category": string or null,
  "summary": string or null
}`;

  let bookData = null;
  let lastError = null;

  for (const model of candidateModels) {
    for (const withSearch of [true, false]) {
      try {
        feedback.textContent = withSearch
          ? `Searching web for ISBN via Gemini (${model})...`
          : `Verifying bibliographic record via Gemini (${model})...`;

        const endpoint = `https://generativelanguage.googleapis.com/v1beta/models/${model}:generateContent?key=${geminiApiKey}`;
        const payload = {
          contents: [{ parts: [{ text: promptText }] }],
          generationConfig: {
            temperature: 0.0
          }
        };
        if (withSearch) {
          payload.tools = [{ googleSearch: {} }];
        }

        const res = await fetch(endpoint, {
          method: "POST",
          headers: { "Content-Type": "application/json" },
          body: JSON.stringify(payload)
        });

        const data = await res.json();
        if (!res.ok || data.error) {
          const errMsg = data.error?.message || `HTTP ${res.status}`;
          console.warn(`Gemini model ${model} (search=${withSearch}) returned error:`, errMsg);

          if (data.error?.status === "INVALID_ARGUMENT" || (errMsg && errMsg.toLowerCase().includes("api key"))) {
            const newKey = prompt(`Gemini API Key was rejected: "${errMsg}".\nPlease enter a new valid Google Gemini API Key:`);
            if (newKey && newKey.trim()) {
              geminiApiKey = newKey.trim();
              await sbClient.from("app_settings").upsert({ key_name: "GeminiApiKey", setting_value: geminiApiKey });
              return performGeminiLookup(isbn);
            }
            throw new Error(errMsg);
          }

          // If search grounding fails with quota (429), retry without search grounding immediately
          if (withSearch && (res.status === 429 || errMsg.toLowerCase().includes("quota"))) {
            continue;
          }

          lastError = new Error(errMsg);
          break; // Try next model
        }

        const rawText = data.candidates?.[0]?.content?.parts?.[0]?.text;
        if (!rawText) {
          lastError = new Error(`Empty response from model ${model}`);
          break;
        }

        const jsonMatch = rawText.match(/\{[\s\S]*\}/);
        if (!jsonMatch) {
          lastError = new Error("Could not parse JSON response from Gemini");
          break;
        }

        bookData = JSON.parse(jsonMatch[0]);
        break; // Success!
      } catch (err) {
        lastError = err;
        break;
      }
    }
    if (bookData) break;
  }

  if (!bookData) {
    feedback.textContent = `AI Lookup Error: ${lastError ? lastError.message : "No response from Gemini"}`;
    feedback.style.color = "var(--danger)";
    return;
  }

  // Check if book was confirmed 100%
  const isFound = bookData && bookData.found === true && bookData.title && bookData.title.trim() !== "";

  if (!isFound) {
    if (warningBanner) {
      warningBanner.style.display = "flex";
      if (warningText) {
        warningText.textContent = "لم يتم العثور على بيانات مؤكدة لهذا الرقم، يرجى إدخال البيانات يدوياً أو مسح الغلاف";
      }
    }
    feedback.textContent = "No verified bibliographic record found for this ISBN.";
    feedback.style.color = "var(--warning)";
    return;
  }

  if (warningBanner) warningBanner.style.display = "none";
  feedback.textContent = `Gemini AI verified "${bookData.title}"! Auto-filling...`;
  feedback.style.color = "var(--accent-green)";

  const categoryStr = bookData.category || bookData.subjects || "";
  const yearStr = bookData.publicationYear || bookData.year || "";
  const summaryStr = bookData.summary || bookData.description || "";

  setTimeout(() => {
    closeScannerModal();
    openAddBookModal();

    document.getElementById("bookTitle").value = bookData.title || "";
    document.getElementById("bookAuthor").value = bookData.author || "";
    document.getElementById("bookPublisher").value = bookData.publisher || "";
    document.getElementById("bookPubYear").value = yearStr;
    document.getElementById("bookPages").value = bookData.pages || "";
    document.getElementById("bookLanguage").value = bookData.language || "Arabic";
    document.getElementById("bookSubjects").value = Array.isArray(categoryStr) ? categoryStr.join(", ") : categoryStr;
    document.getElementById("bookDescription").value = summaryStr;
    document.getElementById("bookISBN").value = bookData.isbn || isbn;
  }, 700);
}

async function performCoverAiScan(base64Image) {
  const feedback = document.getElementById("lookupFeedback");
  const warningBanner = document.getElementById("aiWarningBanner");
  const warningText = document.getElementById("aiWarningText");
  if (warningBanner) warningBanner.style.display = "none";

  if (!geminiApiKey) {
    const key = prompt("Enter your Google Gemini API Key (saved securely to database):");
    if (!key) return;
    geminiApiKey = key.trim();
    await sbClient.from("app_settings").upsert({ key_name: "GeminiApiKey", setting_value: geminiApiKey });
  }

  feedback.textContent = "Analyzing book cover with Gemini AI...";
  feedback.style.color = "var(--accent-purple)";

  const coverPrompt = `You are an expert Arabic and international bibliographic cataloging assistant. Examine this book cover photo carefully.
Extract the book details in its original language and script (especially Arabic for Arabic books).

CRITICAL RULES:
1. Extract the exact book Title printed on the cover.
2. Extract the Author name printed on the cover.
3. Extract the Publisher (دار النشر) if printed on the cover or visible as a logo.
4. Suggest an appropriate category/genre (e.g. رواية، فلسفة، تاريخ، أدب، تنمية بشرية، علوم، دين).
5. If the image is not a book cover or text cannot be reliably read, set "found": false and leave fields null.

Return ONLY a valid JSON object in this exact structure:
{
  "found": boolean,
  "title": string or null,
  "author": string or null,
  "publisher": string or null,
  "publicationYear": string or null,
  "language": string or null,
  "category": string or null,
  "summary": string or null
}`;

  const candidateModels = [
    "gemini-3.5-flash-lite",
    "gemini-3.5-flash",
    "gemini-3.1-flash-lite",
    "gemini-3.8-flash"
  ];

  let bookData = null;
  let lastError = null;

  for (const model of candidateModels) {
    try {
      feedback.textContent = `Recognizing cover with Gemini AI (${model})...`;
      const endpoint = `https://generativelanguage.googleapis.com/v1beta/models/${model}:generateContent?key=${geminiApiKey}`;

      const res = await fetch(endpoint, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({
          contents: [
            {
              parts: [
                { text: coverPrompt },
                {
                  inline_data: {
                    mime_type: "image/jpeg",
                    data: base64Image
                  }
                }
              ]
            }
          ],
          generationConfig: {
            temperature: 0.0
          }
        })
      });

      const data = await res.json();
      if (!res.ok || data.error) {
        lastError = new Error(data.error?.message || `HTTP ${res.status}`);
        continue;
      }

      const rawText = data.candidates?.[0]?.content?.parts?.[0]?.text;
      if (!rawText) continue;

      const jsonMatch = rawText.match(/\{[\s\S]*\}/);
      if (!jsonMatch) continue;

      bookData = JSON.parse(jsonMatch[0]);
      break;
    } catch (err) {
      lastError = err;
    }
  }

  const isFound = bookData && bookData.found === true && bookData.title && bookData.title.trim() !== "";

  if (!isFound) {
    if (warningBanner) {
      warningBanner.style.display = "flex";
      if (warningText) {
        warningText.textContent = "لم نتمكن من قراءة بيانات الغلاف بوضوح، يرجى التقاط صورة أوضح أو إدخال البيانات يدوياً";
      }
    }
    feedback.textContent = "Could not clearly read book title from cover image.";
    feedback.style.color = "var(--warning)";
    return;
  }

  if (warningBanner) warningBanner.style.display = "none";
  feedback.textContent = `Cover recognized: "${bookData.title}"! Auto-filling...`;
  feedback.style.color = "var(--accent-green)";

  const categoryStr = bookData.category || bookData.subjects || "";
  const yearStr = bookData.publicationYear || bookData.year || "";
  const summaryStr = bookData.summary || bookData.description || "";

  setTimeout(() => {
    closeScannerModal();
    openAddBookModal();

    document.getElementById("bookTitle").value = bookData.title || "";
    document.getElementById("bookAuthor").value = bookData.author || "";
    document.getElementById("bookPublisher").value = bookData.publisher || "";
    document.getElementById("bookPubYear").value = yearStr;
    document.getElementById("bookLanguage").value = bookData.language || "Arabic";
    document.getElementById("bookSubjects").value = Array.isArray(categoryStr) ? categoryStr.join(", ") : categoryStr;
    document.getElementById("bookDescription").value = summaryStr;
  }, 700);
}

function captureCameraFrameAndScan() {
  const videoEl = document.querySelector("#interactiveScanner video");
  if (videoEl && videoEl.videoWidth > 0) {
    const canvas = document.createElement("canvas");
    canvas.width = videoEl.videoWidth;
    canvas.height = videoEl.videoHeight;
    const ctx = canvas.getContext("2d");
    ctx.drawImage(videoEl, 0, 0, canvas.width, canvas.height);
    const dataUrl = canvas.toDataURL("image/jpeg", 0.88);
    const base64Data = dataUrl.split(",")[1];
    performCoverAiScan(base64Data);
  } else {
    // If live camera is not running or denied, prompt user to select/take cover image file
    const fileInput = document.getElementById("coverFileInput");
    if (fileInput) fileInput.click();
  }
}

// ============================================================================
// Libraries Management Modal
// ============================================================================
function openLibrariesModal() {
  renderLibrariesList();
  librariesModal.style.display = "flex";
}

function closeLibrariesModal() {
  librariesModal.style.display = "none";
}

function renderLibrariesList() {
  const listEl = document.getElementById("librariesList");
  listEl.innerHTML = "";
  allLibraries.forEach(lib => {
    const li = document.createElement("li");
    li.className = "lib-list-item";
    li.innerHTML = `
      <span><strong>${escapeHtml(lib)}</strong></span>
      ${lib !== "Default Library" ? `<button class="btn btn-secondary btn-sm" style="color:var(--danger);" onclick="deleteLibrary('${escapeQuote(lib)}')">Delete</button>` : `<span style="font-size:0.75rem; color:var(--text-muted);">(Default)</span>`}
    `;
    listEl.appendChild(li);
  });
}

async function handleCreateLibrary() {
  const input = document.getElementById("newLibName");
  const name = input.value.trim();
  if (!name) return;

  try {
    const { error } = await sbClient.from("libraries").insert([{ name }]);
    if (error) throw error;
    input.value = "";
    await loadLibraries();
    renderLibrariesList();
  } catch (err) {
    alert("Error creating library: " + err.message);
  }
}

window.deleteLibrary = async function(name) {
  if (!confirm(`Are you sure you want to delete the library "${name}" and all books inside it?`)) return;

  try {
    const { error } = await sbClient.from("libraries").delete().eq("name", name);
    if (error) throw error;
    if (activeLibrary === name) activeLibrary = "Default Library";
    await loadLibraries();
    await loadBooks();
    renderLibrariesList();
  } catch (err) {
    alert("Error deleting library: " + err.message);
  }
};

// Helpers
function escapeHtml(str) {
  if (!str) return "";
  return String(str)
    .replace(/&/g, "&amp;")
    .replace(/</g, "&lt;")
    .replace(/>/g, "&gt;")
    .replace(/"/g, "&quot;")
    .replace(/'/g, "&#039;");
}

function escapeQuote(str) {
  if (!str) return "";
  return String(str).replace(/'/g, "\\'");
}


// ============================================================================
// Multi-Page Gallery & Lightbox Controller
// ============================================================================
function renderScannedPagesGallery() {
  const grid = document.getElementById("scannedPagesGalleryGrid");
  if (!grid) return;
  grid.innerHTML = "";

  const badge = document.getElementById("scannedPagesBadge");
  if (badge) badge.textContent = `${currentBookPages.length} Pages`;

  if (currentBookPages.length === 0) {
    grid.innerHTML = '<div class="no-pages-hint" id="noPagesHint">No extra pages scanned yet. Click "Scan Page" or "Upload Pages".</div>';
    return;
  }

  currentBookPages.forEach((page, index) => {
    const card = document.createElement("div");
    card.className = "scanned-page-thumb-card";
    card.innerHTML = `
      <div class="page-thumb-img-wrap" onclick="openLightbox('${escapeQuote(page.url)}', '${escapeQuote(page.title || "Page " + (index + 1))}')">
        <img src="${escapeHtml(page.url)}" alt="Page Scan ${index + 1}">
        <span class="page-zoom-badge">🔍 Zoom</span>
      </div>
      <div class="page-thumb-info">
        <span class="page-thumb-title" title="${escapeHtml(page.title || "Page " + (index + 1))}">${escapeHtml(page.title || "Page " + (index + 1))}</span>
        <button type="button" class="page-btn-delete" title="Delete page" onclick="deleteScannedPage(${index})">✕</button>
      </div>
      <div style="display:flex; border-top:1px solid #e2e8f0; font-size:0.7rem;">
        <button type="button" style="flex:1; border:none; background:transparent; padding:4px 0; color:#2563eb; cursor:pointer;" onclick="setPageAsCover(${index})">Set Front</button>
        <button type="button" style="flex:1; border:none; background:transparent; padding:4px 0; border-left:1px solid #e2e8f0; color:#c2410c; cursor:pointer;" onclick="setPageAsBack(${index})">Set Back</button>
      </div>
    `;
    grid.appendChild(card);
  });
}

window.deleteScannedPage = function(index) {
  if (confirm("Remove this scanned page?")) {
    currentBookPages.splice(index, 1);
    renderScannedPagesGallery();
  }
};

window.setPageAsCover = function(index) {
  const page = currentBookPages[index];
  if (!page) return;
  document.getElementById("bookCoverUrl").value = page.url;
  const coverImg = document.getElementById("coverPreviewImg");
  const placeholder = document.getElementById("coverPlaceholderBox");
  if (coverImg) { coverImg.src = page.url; coverImg.style.display = "block"; }
  if (placeholder) placeholder.style.display = "none";
  alert("Page set as Front Cover!");
};

window.setPageAsBack = function(index) {
  const page = currentBookPages[index];
  if (!page) return;
  document.getElementById("bookBackUrl").value = page.url;
  const backImg = document.getElementById("backPreviewImg");
  const placeholder = document.getElementById("backPlaceholderBox");
  if (backImg) { backImg.src = page.url; backImg.style.display = "block"; }
  if (placeholder) placeholder.style.display = "none";
  alert("Page set as Back Cover!");
};

// Lightbox Viewer
function openLightbox(imgSrc, title) {
  const modal = document.getElementById("pageLightboxModal");
  const img = document.getElementById("lightboxImg");
  const caption = document.getElementById("lightboxCaption");
  if (modal && img) {
    img.src = imgSrc;
    if (caption) caption.textContent = title || "";
    modal.style.display = "flex";
  }
}

function closeLightbox() {
  const modal = document.getElementById("pageLightboxModal");
  if (modal) modal.style.display = "none";
}

// ============================================================================
// Full-Screen Camera Viewfinder Controller
// ============================================================================
async function openFullscreenCamera(mode = "front") {
  fsActiveMode = mode;
  updateCameraModeUI();

  const modal = document.getElementById("fullscreenCameraModal");
  if (modal) modal.style.display = "flex";

  await startFullscreenCameraStream();
}

async function startFullscreenCameraStream() {
  const video = document.getElementById("fullscreenVideo");
  if (!video) return;

  if (fsCameraStream) {
    fsCameraStream.getTracks().forEach(t => t.stop());
    fsCameraStream = null;
  }

  try {
    const constraints = {
      video: {
        facingMode: { ideal: fsFacingMode },
        width: { ideal: 1920 },
        height: { ideal: 1080 }
      },
      audio: false
    };

    fsCameraStream = await navigator.mediaDevices.getUserMedia(constraints);
    video.srcObject = fsCameraStream;
    await video.play();

    // Check torch capability
    const track = fsCameraStream.getVideoTracks()[0];
    const btnTorch = document.getElementById("btnToggleTorch");
    if (btnTorch && track) {
      const caps = track.getCapabilities ? track.getCapabilities() : {};
      btnTorch.style.display = caps.torch ? "flex" : "none";
    }
  } catch (err) {
    console.warn("Fullscreen camera stream failed:", err);
    alert("Could not access camera: " + err.message + "\nYou can upload a file instead.");
  }
}

function closeFullscreenCamera() {
  if (fsCameraStream) {
    fsCameraStream.getTracks().forEach(t => t.stop());
    fsCameraStream = null;
  }
  const modal = document.getElementById("fullscreenCameraModal");
  if (modal) modal.style.display = "none";
  const overlay = document.getElementById("cameraProcessingOverlay");
  if (overlay) overlay.style.display = "none";
}

function updateCameraModeUI() {
  const reticleLabel = document.getElementById("cameraReticleLabel");
  document.querySelectorAll(".cam-mode-btn").forEach(btn => {
    btn.classList.toggle("active", btn.getAttribute("data-mode") === fsActiveMode);
  });

  if (reticleLabel) {
    if (fsActiveMode === "front") {
      reticleLabel.textContent = "📖 وجه الكاميرا نحو الغلاف الأمامي / Align Front Cover";
    } else if (fsActiveMode === "back") {
      reticleLabel.textContent = "📄 وجه الكاميرا نحو الغلاف الخلفي والباركود / Align Back Cover";
    } else {
      reticleLabel.textContent = "📑 وجه الكاميرا نحو الصفحة / Align Page within Frame";
    }
  }
}

async function toggleTorch() {
  if (!fsCameraStream) return;
  const track = fsCameraStream.getVideoTracks()[0];
  if (!track || !track.applyConstraints) return;

  fsIsTorchOn = !fsIsTorchOn;
  try {
    await track.applyConstraints({ advanced: [{ torch: fsIsTorchOn }] });
    const btnTorch = document.getElementById("btnToggleTorch");
    if (btnTorch) btnTorch.style.color = fsIsTorchOn ? "#facc15" : "#ffffff";
  } catch (e) {
    console.warn("Torch failed:", e);
  }
}

async function flipCamera() {
  fsFacingMode = fsFacingMode === "environment" ? "user" : "environment";
  await startFullscreenCameraStream();
}

async function handleShutterCapture() {
  const video = document.getElementById("fullscreenVideo");
  const canvas = document.getElementById("fullscreenCanvas");
  if (!video || !canvas || video.videoWidth === 0) {
    alert("Camera not ready. Please wait a moment or upload a photo.");
    return;
  }

  canvas.width = video.videoWidth;
  canvas.height = video.videoHeight;
  const ctx = canvas.getContext("2d");
  ctx.drawImage(video, 0, 0, canvas.width, canvas.height);
  const dataUrl = canvas.toDataURL("image/jpeg", 0.9);
  const base64Data = dataUrl.split(",")[1];

  const overlay = document.getElementById("cameraProcessingOverlay");
  const statusText = document.getElementById("cameraProcessingText");

  if (fsActiveMode === "front") {
    if (overlay && statusText) {
      statusText.textContent = "جاري تحليل الغلاف الأمامي بالذكاء الاصطناعي...";
      overlay.style.display = "flex";
    }
    // Set front cover preview immediately
    document.getElementById("bookCoverUrl").value = dataUrl;
    const coverImg = document.getElementById("coverPreviewImg");
    const placeholder = document.getElementById("coverPlaceholderBox");
    if (coverImg) { coverImg.src = dataUrl; coverImg.style.display = "block"; }
    if (placeholder) placeholder.style.display = "none";

    closeFullscreenCamera();
    closeScannerModal();
    openAddBookModal();
    // Switch to tabMedia
    document.querySelectorAll(".form-tabs .tab-btn").forEach(b => b.classList.remove("active"));
    document.querySelectorAll(".tab-panel").forEach(p => p.classList.remove("active"));
    const tabMediaBtn = document.querySelector(".form-tabs .tab-btn[data-tab='tabMedia']");
    if (tabMediaBtn) tabMediaBtn.classList.add("active");
    const tabMediaPanel = document.getElementById("tabMedia");
    if (tabMediaPanel) tabMediaPanel.classList.add("active");

    await performCoverAiScan(base64Data);

  } else if (fsActiveMode === "back") {
    if (overlay && statusText) {
      statusText.textContent = "جاري تحليل الغلاف الخلفي واستخراج النبذة والباركود...";
      overlay.style.display = "flex";
    }
    document.getElementById("bookBackUrl").value = dataUrl;
    const backImg = document.getElementById("backPreviewImg");
    const placeholder = document.getElementById("backPlaceholderBox");
    if (backImg) { backImg.src = dataUrl; backImg.style.display = "block"; }
    if (placeholder) placeholder.style.display = "none";

    closeFullscreenCamera();
    closeScannerModal();
    openAddBookModal();
    document.querySelectorAll(".form-tabs .tab-btn").forEach(b => b.classList.remove("active"));
    document.querySelectorAll(".tab-panel").forEach(p => p.classList.remove("active"));
    const tabMediaBtn = document.querySelector(".form-tabs .tab-btn[data-tab='tabMedia']");
    if (tabMediaBtn) tabMediaBtn.classList.add("active");
    const tabMediaPanel = document.getElementById("tabMedia");
    if (tabMediaPanel) tabMediaPanel.classList.add("active");

    await performBackCoverAiScan(base64Data);

  } else {
    // Page Scan Mode: Append to multi-page gallery & stay in camera for quick next page!
    currentBookPages.push({
      id: Date.now(),
      title: "Page " + (currentBookPages.length + 1),
      url: dataUrl
    });
    renderScannedPagesGallery();

    // Haptic / Visual Flash feedback
    if (navigator.vibrate) navigator.vibrate(50);
    const badge = document.getElementById("scannedPagesBadge");
    if (badge) {
      badge.textContent = `${currentBookPages.length} Pages`;
      badge.style.color = "#38bdf8";
      setTimeout(() => { badge.style.color = "#ffffff"; }, 600);
    }
  }
}

// ============================================================================
// AI Back-Cover Extraction Engine
// ============================================================================
async function performBackCoverAiScan(base64Image) {
  const feedback = document.getElementById("lookupFeedback");
  const warningBanner = document.getElementById("aiWarningBanner");
  const warningText = document.getElementById("aiWarningText");
  if (warningBanner) warningBanner.style.display = "none";

  if (!geminiApiKey) {
    const key = prompt("Enter your Google Gemini API Key (saved securely to database):");
    if (!key) return;
    geminiApiKey = key.trim();
    await sbClient.from("app_settings").upsert({ key_name: "GeminiApiKey", setting_value: geminiApiKey });
  }

  if (feedback) {
    feedback.textContent = "Analyzing back cover & extracting synopsis/blurb with Gemini AI...";
    feedback.style.color = "var(--accent-purple)";
  }

  const backCoverPrompt = `You are an expert Arabic and international bibliographic cataloging assistant.
Examine this book BACK COVER (الغلاف الخلفي) photo very carefully.

CRITICAL RULES:
1. Extract the ISBN or Market Barcode number: look at the barcode area and text above/below the barcode (e.g., 978..., 977..., or Egyptian 622...). Extract numbers only.
2. Extract the complete Book Summary / Synopsis / Blurb (نبذة عن الكتاب / ملخص الغلاف الخلفي) in its original Arabic text.
3. Extract the book Title if printed on the back cover or in synopsis headers.
4. Extract the Author name and any biographical note about the author on the back cover.
5. Extract the Publisher (دار النشر) and publication year or price (السعر / سنة النشر) if visible.
6. Extract Category / Genre.
7. If this image is not a book back cover or text cannot be reliably read, set "found": false and leave fields null.

Return ONLY a valid JSON object in this exact structure:
{
  "found": boolean,
  "isbn": string or null,
  "title": string or null,
  "author": string or null,
  "publisher": string or null,
  "publicationYear": string or null,
  "language": string or null,
  "category": string or null,
  "summary": string or null
}`;

  const candidateModels = [
    "gemini-3.5-flash-lite",
    "gemini-3.5-flash",
    "gemini-3.1-flash-lite",
    "gemini-3.8-flash"
  ];

  let bookData = null;
  let lastError = null;

  for (const model of candidateModels) {
    try {
      if (feedback) feedback.textContent = `Analyzing back cover with Gemini AI (${model})...`;
      const endpoint = `https://generativelanguage.googleapis.com/v1beta/models/${model}:generateContent?key=${geminiApiKey}`;

      const res = await fetch(endpoint, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({
          contents: [
            {
              parts: [
                { text: backCoverPrompt },
                {
                  inline_data: {
                    mime_type: "image/jpeg",
                    data: base64Image
                  }
                }
              ]
            }
          ],
          generationConfig: {
            temperature: 0.0
          }
        })
      });

      const data = await res.json();
      if (!res.ok || data.error) {
        lastError = new Error(data.error?.message || `HTTP ${res.status}`);
        continue;
      }

      const rawText = data.candidates?.[0]?.content?.parts?.[0]?.text;
      if (!rawText) continue;

      const jsonMatch = rawText.match(/\{[\s\S]*\}/);
      if (!jsonMatch) continue;

      bookData = JSON.parse(jsonMatch[0]);
      break;
    } catch (err) {
      lastError = err;
    }
  }

  const isFound = bookData && bookData.found === true;

  if (!isFound) {
    if (warningBanner) {
      warningBanner.style.display = "flex";
      if (warningText) {
        warningText.textContent = "لم نتمكن من قراءة بيانات الغلاف الخلفي بوضوح، يرجى التقاط صورة أوضح أو كتابة النبذة يدوياً";
      }
    }
    if (feedback) {
      feedback.textContent = "Back cover recognized, but no clear text was extracted.";
      feedback.style.color = "var(--warning)";
    }
    return;
  }

  if (warningBanner) warningBanner.style.display = "none";
  if (feedback) {
    feedback.textContent = `Back cover blurb & data extracted successfully!`;
    feedback.style.color = "var(--accent-green)";
  }

  // Pre-fill form fields
  if (bookData.summary && bookData.summary.trim()) {
    const descEl = document.getElementById("bookDescription");
    if (descEl) descEl.value = bookData.summary.trim();
  }
  if (bookData.isbn && bookData.isbn.trim()) {
    const isbnEl = document.getElementById("bookISBN");
    if (isbnEl && !isbnEl.value) isbnEl.value = bookData.isbn.trim();
  }
  if (bookData.title && bookData.title.trim()) {
    const titleEl = document.getElementById("bookTitle");
    if (titleEl && !titleEl.value) titleEl.value = bookData.title.trim();
  }
  if (bookData.author && bookData.author.trim()) {
    const authEl = document.getElementById("bookAuthor");
    if (authEl && !authEl.value) authEl.value = bookData.author.trim();
  }
  if (bookData.publisher && bookData.publisher.trim()) {
    const pubEl = document.getElementById("bookPublisher");
    if (pubEl && !pubEl.value) pubEl.value = bookData.publisher.trim();
  }
  if (bookData.publicationYear) {
    const yearEl = document.getElementById("bookPubYear");
    if (yearEl && !yearEl.value) yearEl.value = bookData.publicationYear;
  }
}
