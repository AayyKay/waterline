const { app, BrowserWindow, ipcMain, Notification, Menu, Tray, nativeImage, screen } = require('electron');
const path = require('path');
const fs = require('fs');
const { getReminderState } = require('./reminders.cjs');
const { autoUpdater } = require('electron-updater');

let mainWindow;
let widgetWindow;
let widgetCollapsed = false;
let tray;
let reminderTimer;
let reminderInput;
let reminderRuntime = { lastNotificationAt: null };
let updateTimer;
let updateState = { phase: 'idle', currentVersion: app.getVersion(), availableVersion: null, percent: null, lastCheckedAt: null, message: 'Updates are checked automatically.' };
let quitting = false;

const isDev = process.argv.includes('--dev');
const isSmokeTest = process.argv.includes('--smoke-test');
if (isSmokeTest) app.setPath('userData', path.join(app.getPath('temp'), 'waterline-smoke-test'));
const hasSingleInstanceLock = app.requestSingleInstanceLock();
const appUrl = isDev ? 'http://127.0.0.1:5173' : `file://${path.join(__dirname, '..', 'dist', 'index.html').replace(/\\/g, '/')}`;
const iconPath = path.join(__dirname, '..', isDev ? 'public' : 'dist', 'icons', 'waterline-app.png');
const UPDATE_INTERVAL_MS = 4 * 60 * 60 * 1000;

function broadcastUpdateState(patch) {
  updateState = { ...updateState, ...patch };
  for (const win of [mainWindow, widgetWindow]) {
    if (win && !win.isDestroyed()) win.webContents.send('updates:status', updateState);
  }
}

function checkForUpdates(manual = false) {
  if (isDev || !app.isPackaged) {
    broadcastUpdateState({ phase: 'unavailable', message: 'Update checks run in the installed app.', lastCheckedAt: new Date().toISOString() });
    return Promise.resolve(updateState);
  }
  if (manual) broadcastUpdateState({ phase: 'checking', message: 'Checking GitHub Releases…' });
  return autoUpdater.checkForUpdates().catch(error => {
    broadcastUpdateState({ phase: 'error', message: error?.message || 'Could not check for updates.', lastCheckedAt: new Date().toISOString() });
    return null;
  });
}

function initializeAutoUpdater() {
  if (isDev || !app.isPackaged) {
    broadcastUpdateState({ phase: 'unavailable', message: 'Update checks run in the installed app.' });
    return;
  }
  autoUpdater.autoDownload = true;
  autoUpdater.autoInstallOnAppQuit = true;
  autoUpdater.on('checking-for-update', () => broadcastUpdateState({ phase: 'checking', message: 'Checking for updates…' }));
  autoUpdater.on('update-available', info => broadcastUpdateState({ phase: 'available', availableVersion: info.version, percent: 0, message: `Waterline ${info.version} is downloading.` }));
  autoUpdater.on('update-not-available', () => broadcastUpdateState({ phase: 'up-to-date', availableVersion: null, percent: null, lastCheckedAt: new Date().toISOString(), message: 'Waterline is up to date.' }));
  autoUpdater.on('download-progress', progress => broadcastUpdateState({ phase: 'downloading', percent: Math.round(progress.percent), message: `Downloading update… ${Math.round(progress.percent)}%` }));
  autoUpdater.on('update-downloaded', info => {
    broadcastUpdateState({ phase: 'downloaded', availableVersion: info.version, percent: 100, lastCheckedAt: new Date().toISOString(), message: `Waterline ${info.version} is ready to install.` });
    if (Notification.isSupported()) {
      const notification = new Notification({ title: 'Waterline update ready', body: 'Restart Waterline to finish installing the latest version.', icon: iconPath });
      notification.on('click', openMain);
      notification.show();
    }
  });
  autoUpdater.on('error', error => broadcastUpdateState({ phase: 'error', message: error?.message || 'Could not check for updates.', lastCheckedAt: new Date().toISOString() }));
  setTimeout(() => void checkForUpdates(), 12_000);
  updateTimer = setInterval(() => void checkForUpdates(), UPDATE_INTERVAL_MS);
}

function loadRuntime() {
  try {
    reminderRuntime = JSON.parse(fs.readFileSync(path.join(app.getPath('userData'), 'reminder-runtime.json'), 'utf8'));
  } catch { reminderRuntime = { lastNotificationAt: null }; }
}

function saveRuntime() {
  fs.writeFileSync(path.join(app.getPath('userData'), 'reminder-runtime.json'), JSON.stringify(reminderRuntime));
}

function openMain() {
  if (mainWindow && !mainWindow.isDestroyed()) {
    if (mainWindow.isMinimized()) mainWindow.restore();
    mainWindow.show();
    mainWindow.focus();
    return mainWindow;
  }
  mainWindow = new BrowserWindow({
    width: 1280,
    height: 860,
    minWidth: 820,
    minHeight: 620,
    backgroundColor: '#050b1d',
    title: 'Waterline',
    icon: iconPath,
    autoHideMenuBar: true,
    webPreferences: { preload: path.join(__dirname, 'preload.cjs'), contextIsolation: true, nodeIntegration: false },
  });
  void mainWindow.loadURL(appUrl);
  mainWindow.on('close', event => {
    if (!quitting) {
      event.preventDefault();
      mainWindow.hide();
    }
  });
  return mainWindow;
}

function openWidget() {
  if (widgetWindow && !widgetWindow.isDestroyed()) {
    if (widgetCollapsed) setWidgetCollapsed(false);
    widgetWindow.show();
    widgetWindow.focus();
    return widgetWindow;
  }
  const area = screen.getPrimaryDisplay().workArea;
  widgetWindow = new BrowserWindow({
    width: 410,
    height: 490,
    x: area.x + area.width - 430,
    y: area.y + area.height - 510,
    minWidth: 360,
    minHeight: 430,
    maxWidth: 520,
    maxHeight: 620,
    frame: false,
    alwaysOnTop: true,
    skipTaskbar: true,
    resizable: true,
    transparent: true,
    icon: iconPath,
    hasShadow: true,
    webPreferences: { preload: path.join(__dirname, 'preload.cjs'), contextIsolation: true, nodeIntegration: false },
  });
  widgetWindow.setAlwaysOnTop(true, 'floating');
  void widgetWindow.loadURL(`${appUrl}#widget`);
  widgetWindow.on('closed', () => { widgetWindow = undefined; });
  widgetCollapsed = false;
  return widgetWindow;
}

function setWidgetCollapsed(collapsed) {
  if (!widgetWindow || widgetWindow.isDestroyed()) return;
  const area = screen.getDisplayMatching(widgetWindow.getBounds()).workArea;
  const size = collapsed ? { width: 118, height: 118 } : { width: 410, height: 490 };
  widgetCollapsed = collapsed;
  widgetWindow.setResizable(!collapsed);
  if (collapsed) {
    widgetWindow.setMinimumSize(118, 118);
    widgetWindow.setMaximumSize(118, 118);
  } else {
    widgetWindow.setMaximumSize(520, 620);
    widgetWindow.setMinimumSize(360, 430);
  }
  widgetWindow.setBounds({
    x: area.x + area.width - size.width - 20,
    y: area.y + area.height - size.height - 20,
    ...size,
  }, true);
}

function waitForWindow(win) {
  if (!win.webContents.isLoadingMainFrame()) return Promise.resolve();
  return new Promise((resolve, reject) => {
    win.webContents.once('did-finish-load', resolve);
    win.webContents.once('did-fail-load', (_event, code, description) => reject(new Error(`${code}: ${description}`)));
  });
}

function broadcastReminderStatus(status) {
  for (const win of [mainWindow, widgetWindow]) {
    if (win && !win.isDestroyed()) win.webContents.send('reminders:status', status);
  }
}

function scheduleReminder() {
  clearTimeout(reminderTimer);
  if (!reminderInput) return;
  const now = new Date();
  const result = getReminderState({ ...reminderInput, now, lastNotificationAt: reminderRuntime.lastNotificationAt });
  broadcastReminderStatus({ dueAt: result.dueAt?.toISOString() ?? null, active: result.active });
  if (!result.dueAt) return;
  const delay = result.dueAt.getTime() - now.getTime();
  if (delay > 0) {
    reminderTimer = setTimeout(scheduleReminder, Math.min(delay, 2_147_000_000));
    return;
  }
  if (!result.active) {
    reminderTimer = setTimeout(scheduleReminder, 30_000);
    return;
  }

  const remaining = Math.max(0, reminderInput.settings.goal - reminderInput.total);
  const notification = new Notification({
    title: 'Time for a small water break',
    body: `${remaining} oz left today. Take a sip, then log it in Waterline.`,
    silent: true,
    icon: iconPath,
  });
  notification.on('click', () => openMain());
  notification.show();
  const soundWindow = widgetWindow && !widgetWindow.isDestroyed() ? widgetWindow : mainWindow;
  if (soundWindow && !soundWindow.isDestroyed()) soundWindow.webContents.send('reminders:notify');
  reminderRuntime.lastNotificationAt = new Date().toISOString();
  saveRuntime();
  scheduleReminder();
}

if (!hasSingleInstanceLock) {
  app.quit();
} else {
  app.on('second-instance', () => openMain());
  app.whenReady().then(() => {
  app.setAppUserModelId('com.waterline.desktop');
  loadRuntime();
  openMain();
  const trayImage = nativeImage.createFromPath(iconPath);
  tray = new Tray(trayImage.resize({ width: 18, height: 18 }));
  tray.setToolTip('Waterline');
  tray.setContextMenu(Menu.buildFromTemplate([
    { label: 'Open Waterline', click: openMain },
    { label: 'Open mini widget', click: openWidget },
    { type: 'separator' },
    { label: 'Quit', click: () => { quitting = true; app.quit(); } },
  ]));
  tray.on('double-click', openMain);
  initializeAutoUpdater();
  if (isSmokeTest) {
    openWidget();
    Promise.all([waitForWindow(mainWindow), waitForWindow(widgetWindow)])
      .then(async () => {
        const [dashboardOk, widgetOk] = await Promise.all([
          mainWindow.webContents.executeJavaScript("document.body.innerText.includes('waterline')"),
          widgetWindow.webContents.executeJavaScript("document.body.innerText.includes('Log 12 oz')"),
        ]);
        await widgetWindow.webContents.executeJavaScript("[...document.querySelectorAll('button')].find(button => button.textContent.includes('Collapse'))?.click()");
        await new Promise(resolve => setTimeout(resolve, 350));
        const collapsedBounds = widgetWindow.getBounds();
        const widgetCollapseOk = collapsedBounds.width === 118 && collapsedBounds.height === 118;
        await widgetWindow.webContents.executeJavaScript("document.querySelector('[aria-label=\"Expand widget\"]')?.click()");
        await new Promise(resolve => setTimeout(resolve, 350));
        const expandedBounds = widgetWindow.getBounds();
        const widgetExpandOk = expandedBounds.width === 410 && expandedBounds.height === 490;
        const singleInstanceOk = app.hasSingleInstanceLock();
        const passed = dashboardOk && widgetOk && widgetWindow.isAlwaysOnTop() && widgetCollapseOk && widgetExpandOk && singleInstanceOk;
        console.log(JSON.stringify({ dashboardOk, widgetOk, widgetAlwaysOnTop: widgetWindow.isAlwaysOnTop(), widgetCollapseOk, widgetExpandOk, singleInstanceOk }));
        quitting = true;
        app.exit(passed ? 0 : 1);
      })
      .catch(error => { console.error(error); quitting = true; app.exit(1); });
  }
  });
}

ipcMain.handle('window:open-widget', () => openWidget());
ipcMain.handle('window:set-widget-collapsed', (_event, collapsed) => setWidgetCollapsed(Boolean(collapsed)));
ipcMain.handle('window:close-widget', () => widgetWindow?.close());
ipcMain.handle('window:show-dashboard', () => openMain());
ipcMain.handle('updates:get-state', () => updateState);
ipcMain.handle('updates:check', () => checkForUpdates(true));
ipcMain.handle('updates:install', () => {
  if (updateState.phase !== 'downloaded') return false;
  quitting = true;
  autoUpdater.quitAndInstall(false, true);
  return true;
});
ipcMain.handle('window:minimize', event => BrowserWindow.fromWebContents(event.sender)?.minimize());
ipcMain.on('state:changed', (event, state) => {
  for (const win of [mainWindow, widgetWindow]) {
    if (win && !win.isDestroyed() && win.webContents !== event.sender) win.webContents.send('state:changed', state);
  }
});
ipcMain.handle('reminders:sync', (_event, state) => {
  reminderInput = state;
  if (state.lastDrinkAt && reminderRuntime.lastNotificationAt && new Date(state.lastDrinkAt) > new Date(reminderRuntime.lastNotificationAt)) {
    reminderRuntime.lastNotificationAt = null;
    saveRuntime();
  }
  scheduleReminder();
  return { supported: Notification.isSupported() };
});

app.on('activate', openMain);
app.on('before-quit', () => { quitting = true; clearTimeout(reminderTimer); clearInterval(updateTimer); });
app.on('window-all-closed', event => event.preventDefault());
