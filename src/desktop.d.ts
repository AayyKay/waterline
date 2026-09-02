type DesktopDrink = { id: number; amount: number; at: string };
type DesktopSettings = { goal: number; interval: number; start: string; end: string; reminders: boolean; weekdays: number[]; sounds: boolean };
type ReminderStatus = { dueAt: string | null; active: boolean };
type UpdatePhase = 'idle' | 'checking' | 'available' | 'downloading' | 'downloaded' | 'up-to-date' | 'error' | 'unavailable';
type UpdateState = { phase: UpdatePhase; currentVersion: string; availableVersion: string | null; percent: number | null; lastCheckedAt: string | null; message: string };

interface Window {
  waterlineDesktop?: {
    isDesktop: true;
    openWidget: () => Promise<void>;
    setWidgetCollapsed: (collapsed: boolean) => Promise<void>;
    closeWidget: () => Promise<void>;
    showDashboard: () => Promise<void>;
    minimize: () => Promise<void>;
    getUpdateState: () => Promise<UpdateState>;
    checkForUpdates: () => Promise<UpdateState | null>;
    installUpdate: () => Promise<boolean>;
    setReminderState: (state: unknown) => Promise<{ supported: boolean }>;
    syncAppState: (state: unknown) => void;
    onAppState: (callback: (state: { drinkState: { day: string; items: DesktopDrink[] }; settings: DesktopSettings }) => void) => () => void;
    onReminderStatus: (callback: (status: ReminderStatus) => void) => () => void;
    onReminderNotification: (callback: () => void) => () => void;
    onUpdateStatus: (callback: (status: UpdateState) => void) => () => void;
  };
}
