import { useEffect, useMemo, useRef, useState } from 'react';
import { BarChart3, Bell, BellRing, CalendarClock, Check, ChevronDown, ChevronRight, Clock3, Download, Droplet, ExternalLink, GlassWater, Minus, Plus, RefreshCw, Settings2, Target, Undo2, Volume2, Waves, X } from 'lucide-react';

type Drink = { id: number; amount: number; at: string };
type Settings = { goal: number; interval: number; start: string; end: string; reminders: boolean; weekdays: number[]; sounds: boolean };
type Panel = 'schedule' | 'settings' | null;
type ModelContextLike = { registerTool: (tool: Record<string, unknown>, options?: { signal?: AbortSignal }) => void | Promise<void> };
type SoundKind = 'log' | 'reminder';

const DEFAULT_SETTINGS: Settings = { goal: 80, interval: 60, start: '09:00', end: '17:00', reminders: false, weekdays: [1, 2, 3, 4, 5], sounds: true };
const dayKey = (date = new Date()) => `${date.getFullYear()}-${String(date.getMonth() + 1).padStart(2, '0')}-${String(date.getDate()).padStart(2, '0')}`;
const fmtTime = (iso: string) => new Date(iso).toLocaleTimeString([], { hour: 'numeric', minute: '2-digit' });

function load<T>(key: string, fallback: T): T {
  try { return JSON.parse(localStorage.getItem(key) || '') as T; } catch { return fallback; }
}

function playUiSound(kind: SoundKind, enabled: boolean) {
  if (!enabled) return;
  try {
    const AudioContextClass = window.AudioContext || (window as typeof window & { webkitAudioContext?: typeof AudioContext }).webkitAudioContext;
    if (!AudioContextClass) return;
    const context = new AudioContextClass();
    const master = context.createGain();
    master.gain.setValueAtTime(0.0001, context.currentTime);
    master.gain.exponentialRampToValueAtTime(kind === 'log' ? 0.075 : 0.11, context.currentTime + 0.018);
    master.gain.exponentialRampToValueAtTime(0.0001, context.currentTime + (kind === 'log' ? 0.3 : 0.65));
    master.connect(context.destination);
    const notes = kind === 'log' ? [520, 760] : [620, 820, 1040];
    notes.forEach((frequency, index) => {
      const oscillator = context.createOscillator();
      const gain = context.createGain();
      const start = context.currentTime + index * (kind === 'log' ? 0.075 : 0.14);
      oscillator.type = kind === 'log' ? 'sine' : 'triangle';
      oscillator.frequency.setValueAtTime(frequency, start);
      gain.gain.setValueAtTime(0.0001, start);
      gain.gain.exponentialRampToValueAtTime(0.65, start + 0.018);
      gain.gain.exponentialRampToValueAtTime(0.0001, start + 0.2);
      oscillator.connect(gain);
      gain.connect(master);
      oscillator.start(start);
      oscillator.stop(start + 0.22);
    });
    window.setTimeout(() => void context.close(), 900);
  } catch { /* Audio feedback must never block drink logging. */ }
}

function App() {
  const [drinkState, setDrinkState] = useState(() => {
    const day = dayKey();
    return { day, items: load<Drink[]>(`waterline-drinks-${day}`, []) };
  });
  const [settings, setSettings] = useState<Settings>(() => ({ ...DEFAULT_SETTINGS, ...load<Partial<Settings>>('waterline-settings', {}) }));
  const [openPanel, setOpenPanel] = useState<Panel>(null);
  const [customOpen, setCustomOpen] = useState(false);
  const [customAmount, setCustomAmount] = useState(10);
  const [toast, setToast] = useState('');
  const [now, setNow] = useState(() => new Date());
  const [reminderStatus, setReminderStatus] = useState<ReminderStatus>({ dueAt: null, active: false });
  const [updateState, setUpdateState] = useState<UpdateState>({ phase: 'idle', currentVersion: '1.0.0', availableVersion: null, percent: null, lastCheckedAt: null, message: 'Updates are checked automatically.' });
  const [widgetCollapsed, setWidgetCollapsed] = useState(false);
  const [activeSection, setActiveSection] = useState('today');
  const soundEnabled = useRef(settings.sounds);
  const widgetMode = window.location.hash === '#widget';
  const drinks = drinkState.items;

  const total = drinks.reduce((sum, drink) => sum + drink.amount, 0);
  const pct = Math.min(100, Math.round((total / settings.goal) * 100));
  const remaining = Math.max(0, settings.goal - total);
  const isWorkday = settings.weekdays.includes(now.getDay());
  const [startHour, startMin] = settings.start.split(':').map(Number);
  const [endHour, endMin] = settings.end.split(':').map(Number);
  const start = new Date(now); start.setHours(startHour, startMin, 0, 0);
  const end = new Date(now); end.setHours(endHour, endMin, 0, 0);
  const inWorkHours = isWorkday && now >= start && now <= end;
  const scheduleDuration = Math.max(1, end.getTime() - start.getTime());
  const scheduleProgress = !isWorkday || now < start ? 0 : now > end ? 1 : (now.getTime() - start.getTime()) / scheduleDuration;
  const targetNow = Math.round(settings.goal * scheduleProgress);
  const paceDifference = Math.round(total - targetNow);
  const paceTolerance = Math.max(4, Math.round(settings.goal * .05));
  const catchUpAmount = Math.min(remaining, 16, Math.max(8, Math.ceil(Math.max(0, -paceDifference) / 4) * 4));
  const scheduleLabel = `${start.toLocaleTimeString([], { hour: 'numeric', minute: '2-digit' })}–${end.toLocaleTimeString([], { hour: 'numeric', minute: '2-digit' })}`;
  const pace = (() => {
    if (remaining === 0) return { tone: 'complete', title: 'Goal complete', detail: 'You reached today’s goal. Anything else is a bonus.' };
    if (!isWorkday) return { tone: 'quiet', title: 'No plan today', detail: 'Pace is only measured on your selected workdays.' };
    if (now < start) return { tone: 'quiet', title: `Starts at ${start.toLocaleTimeString([], { hour: 'numeric', minute: '2-digit' })}`, detail: 'We’ll compare your intake with an even pace once work hours begin.' };
    if (now > end) return { tone: 'behind', title: `${remaining} oz left`, detail: 'Your workday plan has ended, but you can still finish today.' };
    if (paceDifference < -paceTolerance) return { tone: 'behind', title: `${Math.abs(paceDifference)} oz behind`, detail: `${catchUpAmount} oz now gets you closer to today’s line.` };
    if (paceDifference > paceTolerance) return { tone: 'ahead', title: `${paceDifference} oz ahead`, detail: 'You’ve built some breathing room. Keep sipping normally.' };
    return { tone: 'steady', title: 'Right on schedule', detail: 'Your intake matches an even pace through the workday.' };
  })();
  const lastDrink = drinks.at(-1);
  const nextReminder = reminderStatus.dueAt ? new Date(reminderStatus.dueAt) : null;

  useEffect(() => { soundEnabled.current = settings.sounds; }, [settings.sounds]);
  useEffect(() => { const timer = window.setInterval(() => setNow(new Date()), 1000); return () => window.clearInterval(timer); }, []);
  useEffect(() => {
    const today = dayKey(now);
    if (drinkState.day !== today) setDrinkState({ day: today, items: load<Drink[]>(`waterline-drinks-${today}`, []) });
  }, [now, drinkState.day]);
  useEffect(() => localStorage.setItem(`waterline-drinks-${drinkState.day}`, JSON.stringify(drinks)), [drinkState.day, drinks]);
  useEffect(() => localStorage.setItem('waterline-settings', JSON.stringify(settings)), [settings]);
  useEffect(() => window.waterlineDesktop?.syncAppState({ drinkState, settings }), [drinkState, settings]);
  useEffect(() => window.waterlineDesktop?.onAppState(state => {
    setDrinkState(previous => JSON.stringify(previous) === JSON.stringify(state.drinkState) ? previous : state.drinkState);
    setSettings(previous => JSON.stringify(previous) === JSON.stringify(state.settings) ? previous : { ...DEFAULT_SETTINGS, ...state.settings });
  }), []);
  useEffect(() => {
    const onStorage = (event: StorageEvent) => {
      if (event.key === `waterline-drinks-${drinkState.day}` && event.newValue) {
        try { setDrinkState({ day: drinkState.day, items: JSON.parse(event.newValue) as Drink[] }); } catch { /* Ignore corrupt external data. */ }
      }
      if (event.key === 'waterline-settings' && event.newValue) {
        try { setSettings({ ...DEFAULT_SETTINGS, ...(JSON.parse(event.newValue) as Partial<Settings>) }); } catch { /* Ignore corrupt external data. */ }
      }
    };
    window.addEventListener('storage', onStorage);
    return () => window.removeEventListener('storage', onStorage);
  }, [drinkState.day]);
  useEffect(() => {
    document.documentElement.classList.toggle('widget-mode', widgetMode);
    document.documentElement.classList.toggle('widget-collapsed', widgetMode && widgetCollapsed);
    document.body.classList.toggle('widget-mode', widgetMode);
    document.body.classList.toggle('widget-collapsed', widgetMode && widgetCollapsed);
    return () => {
      document.documentElement.classList.remove('widget-mode', 'widget-collapsed');
      document.body.classList.remove('widget-mode', 'widget-collapsed');
    };
  }, [widgetMode, widgetCollapsed]);
  useEffect(() => {
    if (!toast) return;
    const timer = window.setTimeout(() => setToast(''), 2600);
    return () => clearTimeout(timer);
  }, [toast]);
  useEffect(() => window.waterlineDesktop?.onReminderStatus(setReminderStatus), []);
  useEffect(() => window.waterlineDesktop?.onReminderNotification(() => {
    playUiSound('reminder', soundEnabled.current);
    setToast('Time for a small water break.');
  }), []);
  useEffect(() => {
    const desktop = window.waterlineDesktop;
    if (!desktop) return;
    void desktop.getUpdateState().then(setUpdateState);
    return desktop.onUpdateStatus(status => {
      setUpdateState(status);
      if (status.phase === 'downloaded') setToast(`Waterline ${status.availableVersion ?? ''} is ready to install.`);
    });
  }, []);
  useEffect(() => {
    if (!window.waterlineDesktop) return;
    void window.waterlineDesktop.setReminderState({ settings, total, lastDrinkAt: lastDrink?.at ?? null });
  }, [settings, total, lastDrink?.at]);

  const addDrink = (amount: number) => {
    setDrinkState(previous => ({ ...previous, items: [...previous.items, { id: Date.now(), amount, at: new Date().toISOString() }] }));
    playUiSound('log', soundEnabled.current);
    setToast(`${amount} oz added — nice work.`);
    setCustomOpen(false);
  };

  useEffect(() => {
    const context = (document as Document & { modelContext?: ModelContextLike }).modelContext;
    if (!context?.registerTool) return;
    const lifecycle = new AbortController();
    void Promise.resolve(context.registerTool({
      name: 'log_water', title: 'Log water', description: 'Log a water drink in ounces and update today’s visible hydration progress.',
      inputSchema: { type: 'object', properties: { amountOz: { type: 'number', minimum: 1, maximum: 64 } }, required: ['amountOz'], additionalProperties: false },
      annotations: { readOnlyHint: false, untrustedContentHint: false },
      execute(input: unknown) {
        const amount = Number((input as { amountOz?: unknown })?.amountOz);
        if (!Number.isFinite(amount) || amount < 1 || amount > 64) throw new Error('amountOz must be between 1 and 64.');
        const rounded = Math.round(amount * 10) / 10;
        setDrinkState(previous => ({ ...previous, items: [...previous.items, { id: Date.now(), amount: rounded, at: new Date().toISOString() }] }));
        playUiSound('log', soundEnabled.current);
        setToast(`${rounded} oz added — nice work.`);
        return { loggedOz: rounded, date: dayKey() };
      },
    }, { signal: lifecycle.signal })).catch(() => undefined);
    return () => lifecycle.abort();
  }, []);

  const enableReminders = async () => {
    if (!window.waterlineDesktop) { setToast('Notifications require the Waterline Windows app.'); return; }
    setSettings(previous => ({ ...previous, reminders: true }));
    setToast('Native Windows reminders are on.');
  };

  const toggleWidget = async (collapsed: boolean) => {
    setWidgetCollapsed(collapsed);
    await window.waterlineDesktop?.setWidgetCollapsed(collapsed);
  };

  const goTo = (id: string) => {
    setActiveSection(id);
    document.getElementById(id)?.scrollIntoView({ behavior: 'smooth', block: 'start' });
  };

  const showPanel = (panel: Exclude<Panel, null>) => {
    setActiveSection(panel);
    setOpenPanel(panel);
  };

  const week = useMemo(() => Array.from({ length: 7 }, (_, index) => {
    const date = new Date();
    date.setDate(date.getDate() - (6 - index));
    const key = dayKey(date);
    const value = index === 6 ? total : load<Drink[]>(`waterline-drinks-${key}`, []).reduce((sum, drink) => sum + drink.amount, 0);
    return { label: index === 6 ? 'Today' : date.toLocaleDateString([], { weekday: 'narrow' }), value, hit: value >= settings.goal };
  }), [total, settings.goal]);
  const streak = (() => { let count = 0; for (const day of [...week].reverse()) { if (!day.hit) break; count += 1; } return count; })();

  if (widgetMode && widgetCollapsed) return (
    <main className="collapsed-shell">
      <section className="widget-orb window-drag" aria-label={`${pct}% hydrated today`}>
        <ProgressRing percent={pct} size={92} compact />
        <img src="./icons/waterline-app.png" alt="" />
        {settings.reminders && <span className="notification-badge">1</span>}
        <button className="orb-expand window-no-drag" onClick={() => void toggleWidget(false)} aria-label="Expand widget"><ChevronDown size={16} /></button>
      </section>
    </main>
  );

  if (widgetMode) return (
    <main className="compact-shell">
      <div className="ambient compact-ambient" aria-hidden="true"><span /><span /><span /></div>
      <section className="compact-card">
        <div className="compact-top window-drag"><Brand /><div className="widget-actions window-no-drag"><button className="collapse-button" onClick={() => void toggleWidget(true)}>Collapse <ChevronDown size={15} /></button><button className="icon-button" onClick={() => window.waterlineDesktop?.showDashboard()} aria-label="Open dashboard"><ExternalLink size={16} /></button><button className="icon-button" onClick={() => window.waterlineDesktop?.closeWidget()} aria-label="Close widget"><X size={17} /></button></div></div>
        <div className="compact-progress"><ProgressRing percent={pct} size={144} /><div><span className="eyebrow">TODAY</span><strong>{total} <small>/ {settings.goal} oz</small></strong><p>{remaining ? `${remaining} oz to go` : 'Goal complete!'}</p></div></div>
        <button className="add-main compact-add" onClick={() => addDrink(12)}><GlassWater size={18} /> Log 12 oz</button>
        <div className="quick-row compact-quick"><button onClick={() => addDrink(8)}>+ 8 oz</button><button onClick={() => addDrink(16)}>+ 16 oz</button><button onClick={() => setCustomOpen(true)}>Custom</button></div>
        <div className="widget-divider" />
        <p className="compact-next"><Bell size={15} /> {settings.reminders && nextReminder ? `Next reminder ${fmtTime(nextReminder.toISOString())}` : settings.reminders ? 'No more reminders today' : 'Reminders are off'}</p>
        <div className="mini-pace"><div><strong>{pace.title}</strong><span>{inWorkHours ? `${targetNow} oz due by now` : scheduleLabel}</span></div><div className="pace-line"><span style={{ width: `${pct}%` }} />{inWorkHours && <i style={{ left: `${scheduleProgress * 100}%` }} />}</div></div>
      </section>
      {customOpen && <CustomDialog value={customAmount} setValue={setCustomAmount} onAdd={() => addDrink(customAmount)} onClose={() => setCustomOpen(false)} />}
      {toast && <div className="toast"><Check size={16} />{toast}</div>}
    </main>
  );

  const navItems = [
    { id: 'today', label: 'Today', icon: Waves, action: () => goTo('today') },
    { id: 'insights', label: 'Insights', icon: BarChart3, action: () => goTo('insights') },
    { id: 'schedule', label: 'Schedule', icon: CalendarClock, action: () => showPanel('schedule') },
    { id: 'widget', label: 'Widget', icon: Droplet, action: () => window.waterlineDesktop?.openWidget() },
    { id: 'settings', label: 'Settings', icon: Settings2, action: () => showPanel('settings') },
  ];
  const updateAttention = ['available', 'downloading', 'downloaded'].includes(updateState.phase);

  return (
    <main className="app-shell">
      <div className="ambient" aria-hidden="true"><span /><span /><span /></div>
      <section className="app-frame">
        <aside className="nav-rail" aria-label="Main navigation">
          <button className="nav-brand" onClick={() => goTo('today')} aria-label="Waterline home"><img src="./icons/waterline-app.png" alt="" /></button>
          <nav>{navItems.map(({ id, label, icon: Icon, action }) => <button key={id} className={activeSection === id ? 'active' : ''} onClick={action} aria-label={label} title={label}><Icon size={20} />{id === 'settings' && updateAttention ? <i className="nav-update-dot" /> : null}<span>{label}</span></button>)}</nav>
          <span className={`rail-status ${inWorkHours ? 'active' : ''}`} title={inWorkHours ? 'Workday mode active' : 'Outside work hours'} />
        </aside>

        <div className="app-content">
          <header><Brand /><div className="header-actions"><span className="sync-note"><span className={`status-dot ${inWorkHours ? 'active' : ''}`} />{inWorkHours ? 'Workday mode' : 'Quiet hours'}</span><button className="quiet-button" onClick={() => window.waterlineDesktop?.openWidget()}><Droplet size={17} /> Desktop widget</button></div></header>

          <section className="hero-zone" id="today">
            <div className="wave-ribbon" aria-hidden="true"><span /><span /></div>
            <div className="hero-copy"><span className="eyebrow">{now.toLocaleDateString([], { weekday: 'long' }).toUpperCase()} · DAILY PROGRESS</span><h1>Today</h1><p>{remaining ? 'You’re building a better habit, one sip at a time.' : 'Daily goal reached. Your reminders are resting.'}</p><div className="log-zone"><button className="add-main" onClick={() => addDrink(12)}><GlassWater size={19} /> Log 12 oz</button><div className="quick-row" aria-label="Quick add water"><button onClick={() => addDrink(8)}>+ 8 oz</button><button onClick={() => addDrink(16)}>+ 16 oz</button><button onClick={() => setCustomOpen(true)}>Custom</button></div></div></div>
            <div className="progress-wrap"><ProgressRing percent={pct} size={238} /><div className="progress-label"><strong>{total} <small>/ {settings.goal} oz</small></strong><span>{pct}% of your goal</span></div></div>
            <div className="hero-meta"><Bell size={15} /><span>Next reminder</span><strong>{settings.reminders && nextReminder ? fmtTime(nextReminder.toISOString()) : settings.reminders ? 'Done today' : 'Off'}</strong></div>
          </section>

          <div className="dashboard-grid" id="insights">
            <section className="history-card glass-card"><div className="section-title"><div><span className="eyebrow">LAST 7 DAYS</span><h2>Hydration rhythm</h2></div><span className="streak">{Math.max(0, streak)} day streak <span>↗</span></span></div><div className="bars">{week.map((day, i) => <div className="bar-col" key={i}><div className="bar-track"><span className={day.hit ? 'hit' : ''} style={{ height: `${Math.max(8, Math.min(100, (day.value / settings.goal) * 100))}%` }}>{day.hit && <Check size={12} />}</span></div><small>{day.label}</small></div>)}</div></section>
            <section className={`pace-card glass-card ${pace.tone}`}><div className="section-title"><div><span className="eyebrow">TODAY’S PLAN</span><h2>{pace.title}</h2></div><span className="pace-pill"><Clock3 size={14} />{scheduleLabel}</span></div><div className="pace-comparison"><div><span>Logged</span><strong>{total} <small>oz</small></strong></div><div className="pace-divider" /><div><span>Target by now</span><strong>{targetNow} <small>oz</small></strong></div></div><div className="pace-track" aria-label={`${total} ounces logged; ${targetNow} ounces targeted by now`}><span className="pace-fill" style={{ width: `${pct}%` }} />{isWorkday && <i className="pace-target" style={{ left: `${scheduleProgress * 100}%` }}><b>NOW</b></i>}</div><div className="pace-footer"><p>{pace.detail}</p>{pace.tone === 'behind' && inWorkHours && catchUpAmount > 0 && <button onClick={() => addDrink(catchUpAmount)}><GlassWater size={15} /> Log {catchUpAmount} oz</button>}</div></section>
            <section className="recent-card glass-card"><div className="section-title"><div><span className="eyebrow">TODAY</span><h2>Recent sips</h2></div>{drinks.length > 0 && <button className="text-button" onClick={() => setDrinkState(previous => ({ ...previous, items: previous.items.slice(0, -1) }))}><Undo2 size={14} /> Undo</button>}</div><div className="drink-list">{drinks.length === 0 ? <div className="empty-state"><GlassWater size={23} /><p>Your first glass is one tap away.</p></div> : drinks.slice(-3).reverse().map(drink => <div className="drink-row" key={drink.id}><span className="mini-glass"><Droplet size={15} /></span><div><strong>{drink.amount} oz water</strong><small>{fmtTime(drink.at)}</small></div><Check size={15} /></div>)}</div></section>
            <section className="reminder-card glass-card"><div className="card-heading"><span className="icon-tile"><BellRing size={19} /></span><div><span className="eyebrow">SMART REMINDER</span><h2>{settings.reminders ? 'Next gentle nudge' : 'Stay on your rhythm'}</h2></div></div><strong className="next-time">{settings.reminders && nextReminder ? fmtTime(nextReminder.toISOString()) : settings.reminders ? 'Complete' : 'Off'}</strong><p>{settings.reminders ? 'Logging a drink resets the timer automatically.' : 'Enable native reminders during your work hours.'}</p><button className="secondary-button" onClick={settings.reminders ? () => setSettings(previous => ({ ...previous, reminders: false })) : enableReminders}>{settings.reminders ? 'Pause reminders' : 'Enable notifications'}<ChevronRight size={16} /></button></section>
          </div>
          <footer><p><strong>Waterline</strong> keeps data on this device.</p><button onClick={() => showPanel('schedule')}>Adjust my goal <Target size={14} /></button></footer>
        </div>
      </section>

      {openPanel === 'schedule' && <SchedulePanel settings={settings} setSettings={setSettings} onClose={() => setOpenPanel(null)} />}
      {openPanel === 'settings' && <SettingsPanel settings={settings} setSettings={setSettings} updateState={updateState} onCheckUpdates={() => void window.waterlineDesktop?.checkForUpdates()} onInstallUpdate={() => void window.waterlineDesktop?.installUpdate()} onOpenWidget={() => void window.waterlineDesktop?.openWidget()} onClose={() => setOpenPanel(null)} onEnable={enableReminders} />}
      {customOpen && <CustomDialog value={customAmount} setValue={setCustomAmount} onAdd={() => addDrink(customAmount)} onClose={() => setCustomOpen(false)} />}
      {toast && <div className="toast"><Check size={16} />{toast}</div>}
    </main>
  );
}

function Brand() { return <div className="brand"><img src="./icons/waterline-app.png" alt="" /><div>waterline<small>WORKDAY HYDRATION</small></div></div>; }

function ProgressRing({ percent, size, compact = false }: { percent: number; size: number; compact?: boolean }) {
  const r = 45, c = 2 * Math.PI * r;
  return <svg className={`progress-ring ${compact ? 'compact-ring' : ''}`} width={size} height={size} viewBox="0 0 110 110" role="img" aria-label={`${percent}% of daily water goal`}><defs><linearGradient id={`ring-gradient-${size}`} x1="0" y1="0" x2="1" y2="1"><stop stopColor="#63e6ff" /><stop offset="1" stopColor="#8b7cff" /></linearGradient><filter id={`drop-glow-${size}`}><feGaussianBlur stdDeviation="2.2" result="blur" /><feMerge><feMergeNode in="blur" /><feMergeNode in="SourceGraphic" /></feMerge></filter></defs><circle className="ring-bg" cx="55" cy="55" r={r} /><circle className="ring-fill" cx="55" cy="55" r={r} stroke={`url(#ring-gradient-${size})`} strokeDasharray={c} strokeDashoffset={c - (c * percent / 100)} /><path className="ring-drop" filter={`url(#drop-glow-${size})`} d="M55 29c-6 8-12 14.5-12 22a12 12 0 0 0 24 0c0-7.5-6-14-12-22Z" /></svg>;
}

function SchedulePanel({ settings, setSettings, onClose }: { settings: Settings; setSettings: (s: Settings) => void; onClose: () => void }) {
  const days = [{ n: 'S', v: 0 }, { n: 'M', v: 1 }, { n: 'T', v: 2 }, { n: 'W', v: 3 }, { n: 'T', v: 4 }, { n: 'F', v: 5 }, { n: 'S', v: 6 }];
  return <div className="overlay" onMouseDown={onClose}><aside className="settings-panel" onMouseDown={event => event.stopPropagation()} aria-label="Hydration schedule">
    <div className="panel-head"><div><span className="eyebrow">YOUR ROUTINE</span><h2>Hydration schedule</h2><p>Set the pace for your workday.</p></div><button className="icon-button" onClick={onClose} aria-label="Close schedule"><X size={18} /></button></div>
    <label className="setting-field"><span><Target size={18} /><span><strong>Daily goal</strong><small>Set the amount that works for you.</small></span></span><div className="stepper"><button onClick={() => setSettings({ ...settings, goal: Math.max(24, settings.goal - 8) })} aria-label="Decrease daily goal"><Minus size={15} /></button><strong>{settings.goal} oz</strong><button onClick={() => setSettings({ ...settings, goal: settings.goal + 8 })} aria-label="Increase daily goal"><Plus size={15} /></button></div></label>
    <div className="setting-field block"><span><Clock3 size={18} /><span><strong>Work schedule</strong><small>Reminders stay quiet outside these hours.</small></span></span><div className="time-row"><input aria-label="Workday start time" type="time" value={settings.start} onChange={event => setSettings({ ...settings, start: event.target.value })} /><span>to</span><input aria-label="Workday end time" type="time" value={settings.end} onChange={event => setSettings({ ...settings, end: event.target.value })} /></div><div className="day-row" aria-label="Active reminder days">{days.map((day, i) => <button key={i} className={settings.weekdays.includes(day.v) ? 'selected' : ''} onClick={() => setSettings({ ...settings, weekdays: settings.weekdays.includes(day.v) ? settings.weekdays.filter(d => d !== day.v) : [...settings.weekdays, day.v] })} aria-pressed={settings.weekdays.includes(day.v)}>{day.n}</button>)}</div></div>
    <label className="setting-field"><span><Bell size={18} /><span><strong>Reminder interval</strong><small>Smart reminders reset when you log.</small></span></span><select value={settings.interval} onChange={event => setSettings({ ...settings, interval: Number(event.target.value) })}><option value="30">30 min</option><option value="45">45 min</option><option value="60">1 hour</option><option value="90">90 min</option><option value="120">2 hours</option></select></label>
    <button className="save-button" onClick={onClose}>Save schedule</button>
  </aside></div>;
}

function SettingsPanel({ settings, setSettings, updateState, onCheckUpdates, onInstallUpdate, onOpenWidget, onClose, onEnable }: { settings: Settings; setSettings: (s: Settings) => void; updateState: UpdateState; onCheckUpdates: () => void; onInstallUpdate: () => void; onOpenWidget: () => void; onClose: () => void; onEnable: () => void }) {
  return <div className="overlay" onMouseDown={onClose}><aside className="settings-panel" onMouseDown={event => event.stopPropagation()} aria-label="App settings">
    <div className="panel-head"><div><span className="eyebrow">WATERLINE</span><h2>App settings</h2><p>Manage how Waterline works on your desktop.</p></div><button className="icon-button" onClick={onClose} aria-label="Close settings"><X size={18} /></button></div>
    <section className={`update-card ${updateState.phase}`}>
      <div className="update-icon"><Download size={19} /></div>
      <div className="update-copy"><span className="eyebrow">APP UPDATES · V{updateState.currentVersion}</span><strong>{updateState.phase === 'downloaded' ? 'Update ready' : updateState.phase === 'downloading' || updateState.phase === 'available' ? 'New version available' : updateState.phase === 'checking' ? 'Checking for updates' : updateState.phase === 'up-to-date' ? 'You’re up to date' : 'Automatic updates'}</strong><p>{updateState.message}</p></div>
      {updateState.phase === 'downloaded' ? <button className="update-action primary" onClick={onInstallUpdate}>Restart to update</button> : <button className="update-action" onClick={onCheckUpdates} disabled={updateState.phase === 'checking' || updateState.phase === 'downloading'}><RefreshCw size={14} className={updateState.phase === 'checking' ? 'spin' : ''} /> Check now</button>}
      {updateState.percent !== null && updateState.phase !== 'up-to-date' ? <div className="update-progress"><span style={{ width: `${updateState.percent}%` }} /></div> : null}
    </section>
    <label className="setting-field"><span><Volume2 size={18} /><span><strong>Interface sounds</strong><small>Soft chimes for logging and reminders.</small></span></span><button className={`sound-toggle ${settings.sounds ? 'on' : ''}`} onClick={() => setSettings({ ...settings, sounds: !settings.sounds })} aria-pressed={settings.sounds}><span /></button></label>
    <div className="notification-callout"><BellRing size={20} /><div><strong>{settings.reminders ? 'Notifications enabled' : 'Turn on desk nudges'}</strong><p>{settings.reminders ? 'Waterline will remind you only when you are due.' : 'Uses native Windows notifications.'}</p></div><button onClick={settings.reminders ? () => setSettings({ ...settings, reminders: false }) : onEnable}>{settings.reminders ? 'Turn off' : 'Enable'}</button></div>
    <div className="setting-field widget-setting"><span><Droplet size={18} /><span><strong>Desktop widget</strong><small>Keep today’s progress and quick-add controls nearby.</small></span></span><button className="panel-action" onClick={onOpenWidget}>Open widget <ExternalLink size={14} /></button></div>
    <button className="save-button" onClick={onClose}>Done</button>
  </aside></div>;
}

function CustomDialog({ value, setValue, onAdd, onClose }: { value: number; setValue: (n: number) => void; onAdd: () => void; onClose: () => void }) {
  return <div className="overlay center" onMouseDown={onClose}><div className="custom-dialog" onMouseDown={event => event.stopPropagation()}><button className="icon-button close-custom" onClick={onClose} aria-label="Close custom drink"><X size={18} /></button><span className="icon-tile big"><GlassWater size={25} /></span><span className="eyebrow">CUSTOM DRINK</span><h2>How much water?</h2><div className="custom-stepper"><button onClick={() => setValue(Math.max(1, value - 1))}><Minus /></button><strong>{value}<small> oz</small></strong><button onClick={() => setValue(value + 1)}><Plus /></button></div><button className="save-button" onClick={onAdd}>Add to today</button></div></div>;
}

export default App;
