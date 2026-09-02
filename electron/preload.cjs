const { contextBridge, ipcRenderer } = require('electron');

contextBridge.exposeInMainWorld('waterlineDesktop', {
  isDesktop: true,
  openWidget: () => ipcRenderer.invoke('window:open-widget'),
  setWidgetCollapsed: collapsed => ipcRenderer.invoke('window:set-widget-collapsed', collapsed),
  closeWidget: () => ipcRenderer.invoke('window:close-widget'),
  showDashboard: () => ipcRenderer.invoke('window:show-dashboard'),
  minimize: () => ipcRenderer.invoke('window:minimize'),
  getUpdateState: () => ipcRenderer.invoke('updates:get-state'),
  checkForUpdates: () => ipcRenderer.invoke('updates:check'),
  installUpdate: () => ipcRenderer.invoke('updates:install'),
  setReminderState: state => ipcRenderer.invoke('reminders:sync', state),
  syncAppState: state => ipcRenderer.send('state:changed', state),
  onAppState: callback => {
    const listener = (_event, state) => callback(state);
    ipcRenderer.on('state:changed', listener);
    return () => ipcRenderer.removeListener('state:changed', listener);
  },
  onReminderStatus: callback => {
    const listener = (_event, status) => callback(status);
    ipcRenderer.on('reminders:status', listener);
    return () => ipcRenderer.removeListener('reminders:status', listener);
  },
  onReminderNotification: callback => {
    const listener = () => callback();
    ipcRenderer.on('reminders:notify', listener);
    return () => ipcRenderer.removeListener('reminders:notify', listener);
  },
  onUpdateStatus: callback => {
    const listener = (_event, status) => callback(status);
    ipcRenderer.on('updates:status', listener);
    return () => ipcRenderer.removeListener('updates:status', listener);
  },
});
